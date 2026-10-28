using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace CrankoHub.Services;

/// <summary>
/// Versão Linux do atualizador: procura no GitHub Releases o arquivo CrankoHub-linux-x64.tar.gz,
/// extrai numa pasta ao lado, troca as pastas (o Linux deixa renomear a pasta de um app aberto) e reinicia.
/// Mesma API da versão Windows, para a página Configurações compilar sem mudança.
/// </summary>
public class AtualizacaoService(ArmazenamentoService store, BandejaService bandeja)
{
    private const string Repositorio = "DiegoOttani/MyProjs_";
    private const string PrefixoTag = "crankohub-v";
    private const string NomeArquivo = "CrankoHub-linux-x64.tar.gz";

    public const string ArgAtualizado = "--atualizado";

    public static Version VersaoAtual { get; } = Normalizar(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0));

    private static readonly HttpClient Http = CriarHttp();

    private Timer? _timer;

    public record Novidade(Version Versao, string Notas, long ArquivoId, long Tamanho);

    public Novidade? Disponivel { get; private set; }

    /// <summary>Só a versão publicada (pasta com o executável CrankoHub) pode se substituir.</summary>
    public static bool PodeInstalar => Environment.ProcessPath is string exe && Path.GetFileName(exe) == "CrankoHub";

    public static string Texto(Version v) => v.ToString(3);

    public async Task<Novidade?> VerificarAsync()
    {
        using var req = Requisicao($"https://api.github.com/repos/{Repositorio}/releases?per_page=30", "application/vnd.github+json");
        using var resp = await Http.SendAsync(req);
        if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            throw new InvalidOperationException("Token inválido ou sem acesso ao repositório.");
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

    public async Task InstalarAsync(Novidade n, IProgress<double>? progresso = null)
    {
        if (!PodeInstalar)
            throw new InvalidOperationException("A atualização só funciona na versão publicada do CrankoHub, não rodando pelo dotnet.");
        var pasta = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var nova = pasta + ".new";
        var antiga = pasta + ".old";
        var pacote = Path.Combine(Path.GetTempPath(), NomeArquivo);

        using (var req = Requisicao($"https://api.github.com/repos/{Repositorio}/releases/assets/{n.ArquivoId}", "application/octet-stream"))
        using (var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? n.Tamanho;
            await using var origem = await resp.Content.ReadAsStreamAsync();
            await using var destino = File.Create(pacote);
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
        if (new FileInfo(pacote).Length != n.Tamanho)
        {
            File.Delete(pacote);
            throw new InvalidOperationException("O download veio incompleto. Tente de novo.");
        }

        if (Directory.Exists(nova)) Directory.Delete(nova, true);
        Directory.CreateDirectory(nova);
        await using (var arquivo = File.OpenRead(pacote))
        await using (var gz = new GZipStream(arquivo, CompressionMode.Decompress))
            await TarFile.ExtractToDirectoryAsync(gz, nova, overwriteFiles: true);
        File.Delete(pacote);
        if (!File.Exists(Path.Combine(nova, "CrankoHub")))
            throw new InvalidOperationException("O pacote baixado não contém o CrankoHub.");

        if (Directory.Exists(antiga)) Directory.Delete(antiga, true);
        Directory.Move(pasta, antiga);
        try { Directory.Move(nova, pasta); }
        catch { Directory.Move(antiga, pasta); throw; }

        // A trava de instância única é liberada quando este processo fecha; o novo espera por isso
        Process.Start(new ProcessStartInfo(Path.Combine(pasta, "CrankoHub"), $"{ArgAtualizado} {Environment.ProcessId}") { UseShellExecute = false });
        DialogoService.Janela?.Close();
    }

    public void Iniciar()
    {
        _timer = new Timer(async _ =>
        {
            var cfg = store.Dados.Config;
            if (!cfg.VerificarAtualizacoes || string.IsNullOrWhiteSpace(cfg.TokenGitHub) || cfg.UltimaVerificacaoAtualizacao == Formatacao.Hoje)
                return;
            try
            {
                if (await VerificarAsync() is Novidade n)
                    bandeja.Notificar("Nova versão do CrankoHub", $"A versão {Texto(n.Versao)} está disponível em Configurações → Atualizações.");
            }
            catch { } // sem internet ou token inválido: tenta de novo mais tarde
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromHours(6));
    }

    /// <summary>Depois de uma atualização: espera a versão antiga fechar e apaga a pasta antiga.</summary>
    public static bool PrepararInicio(string[] args)
    {
        var i = Array.IndexOf(args, ArgAtualizado);
        var atualizado = i >= 0;
        if (atualizado && i + 1 < args.Length && int.TryParse(args[i + 1], out var pid))
            try { using var p = Process.GetProcessById(pid); p.WaitForExit(15000); } catch { }

        var pasta = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        foreach (var sobra in new[] { pasta + ".old", pasta + ".new" })
            try { if (Directory.Exists(sobra)) Directory.Delete(sobra, true); } catch { }
        return atualizado;
    }

    private HttpRequestMessage Requisicao(string url, string aceita)
    {
        var token = store.Dados.Config.TokenGitHub;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Cole o token do GitHub nas Configurações para verificar atualizações.");
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
