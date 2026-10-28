using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows.Threading;

namespace CrankoHub.Services;

/// <summary>
/// Procura versões novas no GitHub Releases (tags "crankohub-vX.Y.Z", criadas pelo workflow
/// .github/workflows/crankohub-release.yml), baixa o novo CrankoHub.exe, troca pelo atual e reinicia.
/// O repositório é privado, então as chamadas usam o token salvo nas Configurações.
/// </summary>
public class AtualizacaoService(ArmazenamentoService store, BandejaService bandeja)
{
    private const string Repositorio = "DiegoOttani/cranko-hub";
    private const string PrefixoTag = "crankohub-v";
    private const string NomeArquivo = "CrankoHub.exe";

    /// <summary>Passado ao novo processo com o PID do antigo, para esperar ele fechar.</summary>
    public const string ArgAtualizado = "--atualizado";

    /// <summary>Reinício pedido pelo próprio app (ex.: trocou a pasta dos dados): espera a versão atual fechar.</summary>
    public const string ArgReiniciado = "--reiniciado";

    // Declarada antes do Http, que a usa no User-Agent (inicializadores estáticos rodam em ordem)
    public static Version VersaoAtual { get; } = Normalizar(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0));

    // Cliente próprio: o download (~100 MB) não cabe no timeout de 15 s do HttpClient compartilhado
    private static readonly HttpClient Http = CriarHttp();

    private DispatcherTimer? _timer;

    public record Novidade(Version Versao, string Notas, long ArquivoId, long Tamanho);

    /// <summary>Última versão nova encontrada (null se está em dia ou ainda não verificou).</summary>
    public Novidade? Disponivel { get; private set; }

    /// <summary>Só o .exe publicado (arquivo único) pode se substituir; rodando pelo Visual Studio, não.</summary>
    public static bool PodeInstalar => string.IsNullOrEmpty(typeof(App).Assembly.Location) && Environment.ProcessPath is not null;

    public static string Texto(Version v) => v.ToString(3);

    /// <summary>Consulta o GitHub. Retorna a versão mais nova que a instalada, ou null se já está em dia.</summary>
    public async Task<Novidade?> VerificarAsync()
    {
        using var req = Requisicao($"https://api.github.com/repos/{Repositorio}/releases?per_page=30", "application/vnd.github+json");
        using var resp = await Http.SendAsync(req);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            throw new InvalidOperationException("Não foi possível consultar o repositório no GitHub (limite de consultas ou sem acesso). Tente mais tarde.");
        resp.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        Novidade? melhor = null;
        foreach (var r in json.RootElement.EnumerateArray())
        {
            if (r.GetProperty("draft").GetBoolean() || r.GetProperty("prerelease").GetBoolean()) continue;
            var tag = r.GetProperty("tag_name").GetString() ?? "";
            if (!tag.StartsWith(PrefixoTag, StringComparison.OrdinalIgnoreCase) || !Version.TryParse(tag[PrefixoTag.Length..], out var v)) continue;
            v = Normalizar(v);
            if (v <= VersaoAtual || (melhor is not null && v <= melhor.Versao)) continue;
            foreach (var a in r.GetProperty("assets").EnumerateArray())
                if (a.GetProperty("name").GetString() == NomeArquivo)
                    melhor = new Novidade(v, r.GetProperty("body").GetString() ?? "", a.GetProperty("id").GetInt64(), a.GetProperty("size").GetInt64());
        }

        Disponivel = melhor;
        store.Dados.Config.UltimaVerificacaoAtualizacao = Formatacao.Hoje;
        store.Salvar();
        return melhor;
    }

    /// <summary>Baixa a versão nova, troca o .exe (o Windows deixa renomear um .exe aberto) e reinicia o app.</summary>
    public async Task InstalarAsync(Novidade n, IProgress<double>? progresso = null)
    {
        if (!PodeInstalar)
            throw new InvalidOperationException("A atualização só funciona no CrankoHub.exe publicado, não rodando pelo Visual Studio.");
        var exe = Environment.ProcessPath!;
        var novo = exe + ".new";
        var antigo = exe + ".old";

        using (var req = Requisicao($"https://api.github.com/repos/{Repositorio}/releases/assets/{n.ArquivoId}", "application/octet-stream"))
        using (var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? n.Tamanho;
            await using var origem = await resp.Content.ReadAsStreamAsync();
            await using var destino = File.Create(novo);
            var buffer = new byte[81920];
            long lidos = 0;
            int qtd;
            while ((qtd = await origem.ReadAsync(buffer)) > 0)
            {
                await destino.WriteAsync(buffer.AsMemory(0, qtd));
                lidos += qtd;
                if (total > 0) progresso?.Report((double)lidos / total);
            }
        }
        if (new FileInfo(novo).Length != n.Tamanho)
        {
            File.Delete(novo);
            throw new InvalidOperationException("O download veio incompleto. Tente de novo.");
        }

        File.Delete(antigo);
        File.Move(exe, antigo);
        try { File.Move(novo, exe); }
        catch { File.Move(antigo, exe); throw; }

        Process.Start(new ProcessStartInfo(exe, $"{ArgAtualizado} {Environment.ProcessId}") { UseShellExecute = false });
        System.Windows.Application.Current.Dispatcher.Invoke(() => ((App)System.Windows.Application.Current).Sair());
    }

    /// <summary>Fecha e abre o app de novo.</summary>
    public static void Reiniciar()
    {
        if (Environment.ProcessPath is not string exe) return;
        Process.Start(new ProcessStartInfo(exe, $"{ArgReiniciado} {Environment.ProcessId}") { UseShellExecute = false });
        System.Windows.Application.Current.Dispatcher.Invoke(() => ((App)System.Windows.Application.Current).Sair());
    }

    /// <summary>Verifica sozinho uma vez por dia (logo depois de abrir e, com o app na bandeja, a cada 6 h).</summary>
    public void Iniciar()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += async (_, _) =>
        {
            _timer.Interval = TimeSpan.FromHours(6);
            var cfg = store.Dados.Config;
            if (!cfg.VerificarAtualizacoes || cfg.UltimaVerificacaoAtualizacao == Formatacao.Hoje)
                return;
            try
            {
                if (await VerificarAsync() is Novidade n)
                    bandeja.Notificar("Nova versão do CrankoHub", $"A versão {Texto(n.Versao)} está disponível. Clique para atualizar.", "configuracoes");
            }
            catch { } // sem internet ou token inválido: tenta de novo mais tarde, sem incomodar
        };
        _timer.Start();
    }

    /// <summary>
    /// Ao abrir depois de uma atualização: espera a versão antiga fechar (ela ainda segura o mutex e o WebView2)
    /// e apaga as sobras da troca. Retorna true se o app acabou de ser atualizado.
    /// </summary>
    public static bool PrepararInicio(string[] args)
    {
        var r = Array.IndexOf(args, ArgReiniciado);
        if (r >= 0 && r + 1 < args.Length && int.TryParse(args[r + 1], out var anterior))
            try { using var p = Process.GetProcessById(anterior); p.WaitForExit(15000); } catch { }

        var i = Array.IndexOf(args, ArgAtualizado);
        var atualizado = i >= 0;
        if (atualizado && i + 1 < args.Length && int.TryParse(args[i + 1], out var pid))
            try { using var p = Process.GetProcessById(pid); p.WaitForExit(15000); } catch { }

        if (Environment.ProcessPath is string exe)
            foreach (var sobra in new[] { exe + ".old", exe + ".new" })
                try { File.Delete(sobra); } catch { }
        return atualizado;
    }

    private HttpRequestMessage Requisicao(string url, string aceita)
    {
        // Repositório público: o GitHub responde sem token (limite de 60 consultas por hora por IP)
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd(aceita);
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return req;
    }

    private static HttpClient CriarHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CrankoHub/" + Texto(VersaoAtual));
        return http;
    }

    private static Version Normalizar(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
