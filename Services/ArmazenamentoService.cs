using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>
/// Guarda os dados do aplicativo em %LocalAppData%\CrankoHub\dados.json e cuida de
/// backup (exportar/importar), dados de exemplo e "apagar tudo".
/// </summary>
public class ArmazenamentoService
{
    public static readonly string PastaDados = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrankoHub");

    public static readonly string CaminhoDados = Path.Combine(PastaDados, "dados.json");

    // Mesmo formato (camelCase, datas "AAAA-MM-DD") do antigo app web, para os backups serem compatíveis
    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // mantém acentos legíveis no arquivo
    };

    private static readonly JsonSerializerOptions OpcoesJsonIndentado = new(OpcoesJson) { WriteIndented = true };

    private readonly ToastService _toast;

    public ArmazenamentoService(ToastService toast)
    {
        _toast = toast;
        Dados = Carregar();
    }

    public EstadoApp Dados { get; private set; }

    /// <summary>Avisa o menu quando um módulo é mostrado ou escondido.</summary>
    public event Action? ModulosMudaram;

    public void MostrarModulo(string id, bool visivel)
    {
        Dados.Config.ModulosOcultos.Remove(id);
        if (!visivel) Dados.Config.ModulosOcultos.Add(id);
        Salvar();
        ModulosMudaram?.Invoke();
    }

    /// <summary>Id curto e único (mesmo estilo do app web).</summary>
    public static string NovoId()
    {
        const string base36 = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new StringBuilder();
        for (var t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); t > 0; t /= 36)
            sb.Insert(0, base36[(int)(t % 36)]);
        for (var i = 0; i < 5; i++)
            sb.Append(base36[Random.Shared.Next(36)]);
        return sb.ToString();
    }

    public void Salvar()
    {
        try
        {
            Directory.CreateDirectory(PastaDados);
            // Grava num arquivo temporário e troca no final, para nunca deixar o arquivo pela metade
            var temporario = CaminhoDados + ".tmp";
            File.WriteAllText(temporario, JsonSerializer.Serialize(Dados, OpcoesJsonIndentado));
            File.Move(temporario, CaminhoDados, overwrite: true);
        }
        catch (Exception ex)
        {
            _toast.Erro($"Não foi possível salvar os dados: {ex.Message}");
        }
    }

    public void Resetar()
    {
        var (tema, config) = (Dados.Tema, Dados.Config);
        Dados = Novo();
        Dados.Tema = tema;
        Dados.Config = config;
        Salvar();
    }

    public void CarregarExemplo()
    {
        var config = Dados.Config;
        Dados = Exemplo(Dados.Tema);
        Dados.Config = config;
        Salvar();
    }

    /// <summary>Substitui todos os dados pelo conteúdo de um backup .json.</summary>
    /// <exception cref="InvalidDataException">Se o arquivo não for um backup válido.</exception>
    public void Importar(string json)
    {
        EstadoApp? estado;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("dieta", out _) || !raiz.TryGetProperty("carro", out _))
                throw new InvalidDataException("Arquivo não parece ser um backup do CrankoHub.");
            estado = raiz.Deserialize<EstadoApp>(OpcoesJson);
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Arquivo inválido.");
        }

        // As configurações (pasta de backup etc.) são deste computador: não vêm do arquivo
        var config = Dados.Config;
        Dados = Normalizar(estado ?? throw new InvalidDataException("Arquivo inválido."));
        Dados.Config = config;
        Salvar();
    }

    public string Exportar() => JsonSerializer.Serialize(Dados, OpcoesJsonIndentado);

    public double TamanhoKB() => Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(Dados, OpcoesJson)) / 1024.0;

    private EstadoApp Carregar()
    {
        try
        {
            if (File.Exists(CaminhoDados))
            {
                var estado = JsonSerializer.Deserialize<EstadoApp>(File.ReadAllText(CaminhoDados), OpcoesJson);
                if (estado is not null)
                    return Normalizar(estado);
            }
        }
        catch
        {
            // Arquivo corrompido: guarda uma cópia para não perder nada e começa do zero
            try { File.Copy(CaminhoDados, CaminhoDados + $".corrompido-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true); } catch { }
        }
        return Novo();
    }

    private static EstadoApp Novo()
    {
        var veiculo = new Veiculo { Id = NovoId(), Nome = "Meu carro" };
        var quadro = TarefasCalculos.NovoQuadro("Pessoal", "🏠");
        return new EstadoApp
        {
            Carro = new DadosCarro { Veiculos = [veiculo], Ativo = veiculo.Id },
            Tarefas = new DadosTarefas { Quadros = [quadro], Ativo = quadro.Id },
        };
    }

    // Garante que nada essencial esteja faltando (arquivos antigos ou editados à mão)
    private static EstadoApp Normalizar(EstadoApp s)
    {
        s.Dieta ??= new DadosDieta();
        s.Dieta.Perfil ??= new PerfilDieta();
        s.Dieta.Registros ??= [];
        s.Dieta.Registros.RemoveAll(r => r is null);
        s.Carro ??= new DadosCarro();
        s.Carro.Abastecimentos ??= [];
        s.Carro.Abastecimentos.RemoveAll(a => a is null);
        s.Carro.Manutencoes ??= []; // backups antigos (e do app web) não têm manutenções
        s.Carro.Manutencoes.RemoveAll(m => m is null);
        s.Carro.Documentos ??= [];
        s.Carro.Documentos.RemoveAll(x => x is null);
        s.Carro.Veiculos ??= [];
        s.Carro.Veiculos.RemoveAll(v => v is null);
        if (s.Carro.Veiculos.Count == 0)
            s.Carro.Veiculos = Novo().Carro.Veiculos;
        if (!s.Carro.Veiculos.Any(v => v.Id == s.Carro.Ativo))
            s.Carro.Ativo = s.Carro.Veiculos[0].Id;
        s.Config ??= new Configuracoes();
        s.Config.AvisosEnviados ??= [];
        s.Config.ModulosOcultos ??= [];
        s.Config.ModulosOcultos.RemoveAll(id => Modulo.Buscar(id) is null);
        s.Assinaturas ??= []; // backups antigos (e do app web) não têm assinaturas
        s.Assinaturas.RemoveAll(a => a is null);
        s.Entretenimento ??= [];
        s.Entretenimento.RemoveAll(e => e is null);

        foreach (var r in s.Dieta.Registros.Where(r => string.IsNullOrEmpty(r.Id))) r.Id = NovoId();
        foreach (var a in s.Carro.Abastecimentos.Where(a => string.IsNullOrEmpty(a.Id))) a.Id = NovoId();
        foreach (var m in s.Carro.Manutencoes.Where(m => string.IsNullOrEmpty(m.Id))) m.Id = NovoId();
        foreach (var x in s.Carro.Documentos.Where(x => string.IsNullOrEmpty(x.Id))) x.Id = NovoId();
        foreach (var a in s.Assinaturas.Where(a => string.IsNullOrEmpty(a.Id))) a.Id = NovoId();
        foreach (var e in s.Entretenimento.Where(e => string.IsNullOrEmpty(e.Id))) e.Id = NovoId();

        // Tarefas: backups antigos não têm; sempre deixa pelo menos um quadro com colunas
        var t = s.Tarefas ??= new DadosTarefas();
        t.Quadros ??= [];
        t.Quadros.RemoveAll(q => q is null);
        foreach (var q in t.Quadros)
        {
            if (string.IsNullOrEmpty(q.Id)) q.Id = NovoId();
            q.Colunas ??= [];
            q.Colunas.RemoveAll(c => c is null);
            foreach (var c in q.Colunas.Where(c => string.IsNullOrEmpty(c.Id))) c.Id = NovoId();
            if (q.Colunas.Count == 0) q.Colunas = TarefasCalculos.NovoQuadro(q.Nome).Colunas;
        }
        if (t.Quadros.Count == 0)
            t.Quadros.Add(TarefasCalculos.NovoQuadro("Pessoal", "🏠"));
        if (t.Ativo != Quadro.Hoje && t.Quadros.All(q => q.Id != t.Ativo))
            t.Ativo = t.Quadros[0].Id;
        t.Cards ??= [];
        t.Cards.RemoveAll(c => c is null || t.Quadros.All(q => q.Id != c.QuadroId));
        foreach (var c in t.Cards)
        {
            if (string.IsNullOrEmpty(c.Id)) c.Id = NovoId();
            c.Etiquetas ??= [];
            c.Checklist ??= [];
            c.Checklist.RemoveAll(i => i is null);
            var q = t.Quadros.First(q => q.Id == c.QuadroId);
            if (q.Colunas.All(col => col.Id != c.ColunaId)) c.ColunaId = q.Colunas[0].Id;
        }
        return s;
    }

    /// <summary>~5 meses de pesos, ~8 meses de abastecimentos e manutenções e algumas assinaturas fictícias para explorar o app.</summary>
    private static EstadoApp Exemplo(string? tema)
    {
        var s = Novo();
        s.Tema = tema;
        var hoje = Formatacao.Hoje;
        var rnd = Random.Shared;
        DateOnly Dia(int n) => hoje.AddDays(-n);
        double R(double a, double b) => a + rnd.NextDouble() * (b - a);
        string[] notas = ["Treino de perna", "Refeição livre no fim de semana", "Dormi mal", "Caminhada de 5 km", "Dia cheio, comi pouco"];

        s.Dieta.Perfil = new PerfilDieta { Altura = 178, Meta = 80 };
        var ultimaMedida = 999;
        for (var d = 150; d >= 0; d -= 1 + rnd.Next(3))
        {
            var t = 150 - d;
            var reg = new RegistroPeso { Id = NovoId(), Data = Dia(d), Peso = Math.Round(92.4 - t * 0.055 + R(-0.4, 0.4), 1) };
            if (ultimaMedida - d >= 14 || ultimaMedida == 999)
            {
                reg.Gordura = Math.Round(27.8 - t * 0.04 + R(-0.3, 0.3), 1);
                reg.Cintura = Math.Round(103 - t * 0.045 + R(-0.5, 0.5), 1);
                ultimaMedida = d;
            }
            if (rnd.NextDouble() < 0.15) reg.Obs = notas[rnd.Next(notas.Length)];
            s.Dieta.Registros.Add(reg);
        }

        var vid = s.Carro.Veiculos[0].Id;
        string[] postos = ["Shell Centro", "Ipiranga Av. Brasil", "Posto BR Rodovia", "Ale Bairro"];
        double odo = 48200;
        for (var d = 240; d >= 0; d -= 6 + rnd.Next(5))
        {
            var t = 240 - d;
            var gasolina = rnd.NextDouble() >= 0.35;
            var litros = Math.Round(R(30, 42), 2);
            var consumo = gasolina ? R(10.9, 12.7) : R(7.6, 8.8);
            var preco = gasolina ? 5.79 + t * 0.0016 + R(-0.08, 0.08) : 3.99 + t * 0.0012 + R(-0.08, 0.08);
            var km = Math.Round(litros * consumo, 1);
            odo += km;
            s.Carro.Abastecimentos.Add(new Abastecimento
            {
                Id = NovoId(), VeiculoId = vid, Data = Dia(d), Combustivel = gasolina ? "gasolina" : "etanol",
                Odometro = Math.Round(odo), Km = km, Litros = litros, Valor = Math.Round(litros * preco, 2),
                Posto = postos[rnd.Next(postos.Length)],
            });
        }

        // Odômetro aproximado num dia (pelo abastecimento mais próximo antes dele)
        double OdometroEm(int dias) =>
            s.Carro.Abastecimentos.LastOrDefault(a => a.Data <= Dia(dias))?.Odometro ?? 48200;
        Manutencao Manut(string tipo, int dias, double valor, string descricao, string local)
        {
            var t = TipoManutencao.Buscar(tipo);
            return new()
            {
                Id = NovoId(), VeiculoId = vid, Tipo = tipo, Data = Dia(dias), Odometro = Math.Round(OdometroEm(dias)),
                Valor = valor, Descricao = descricao, Local = local, IntervaloKm = t.Km, IntervaloMeses = t.Meses,
            };
        }
        s.Carro.Manutencoes =
        [
            Manut("oleo", 230, 289.90, "Óleo 5W30 sintético + filtro de óleo", "Auto Center Silva"),
            Manut("alinhamento", 200, 160.00, "Alinhamento e balanceamento das 4 rodas", "Pneus & Cia"),
            Manut("revisao", 150, 780.00, "Revisão dos 50 mil km", "Concessionária"),
            Manut("filtros", 150, 145.00, "Filtro de ar e de cabine", "Concessionária"),
            Manut("oleo", 95, 299.90, "Óleo 5W30 sintético + filtro de óleo", "Auto Center Silva"),
            Manut("pneus", 60, 80.00, "Rodízio dos pneus", "Pneus & Cia"),
            Manut("bateria", 700, 520.00, "Bateria 60 Ah", "Loja de baterias"),
        ];

        DocumentoCarro Doc(string tipo, int diasAteVencer, double? valor, string? descricao, bool pago = false) =>
            new() { Id = NovoId(), VeiculoId = tipo == "cnh" ? null : vid, Tipo = tipo, Vencimento = hoje.AddDays(diasAteVencer), Valor = valor, Descricao = descricao, Pago = pago };
        s.Carro.Documentos =
        [
            Doc("ipva", -60, 812.40, "Parcela 1/3", pago: true),
            Doc("ipva", -30, 812.40, "Parcela 2/3", pago: true),
            Doc("ipva", 5, 812.40, "Parcela 3/3"),
            Doc("licenciamento", 45, 160.22, null),
            Doc("seguro", 120, 2890.00, "Porto Seguro · franquia R$ 3.500"),
            Doc("cnh", 800, null, "Categoria B"),
        ];

        Assinatura Assin(string nome, double valor, string ciclo, int diasAteVencer, string categoria, string pagamento, bool ativa = true) =>
            new() { Id = NovoId(), Nome = nome, Valor = valor, Ciclo = ciclo, Vencimento = hoje.AddDays(diasAteVencer), Categoria = categoria, Pagamento = pagamento, Ativa = ativa };
        s.Assinaturas =
        [
            Assin("Netflix", 44.90, "mensal", 3, "Streaming", "Cartão de crédito"),
            Assin("Spotify", 21.90, "mensal", 12, "Música", "Cartão de crédito"),
            Assin("Disney+", 33.90, "mensal", 0, "Streaming", "Cartão de crédito"),
            Assin("Google One 2 TB", 349.99, "anual", 140, "Nuvem", "Cartão de crédito"),
            Assin("Microsoft 365", 319.00, "anual", 52, "Software", "Pix"),
            Assin("Academia", 129.90, "mensal", 6, "Academia", "Débito automático"),
            Assin("Xbox Game Pass", 59.99, "mensal", 21, "Jogos", "Cartão de crédito"),
            Assin("Jornal digital", 9.90, "mensal", 17, "Notícias", "Cartão de crédito", ativa: false),
        ];

        ItemEntretenimento Ent(string cat, string titulo, int ano, int? diasAtras, double? nota, string status = "concluido", string? criadores = null) =>
            new() { Id = NovoId(), Categoria = cat, Titulo = titulo, Ano = ano, Data = diasAtras is int d ? Dia(d) : null, Nota = nota, Status = status, Criadores = criadores };
        s.Entretenimento =
        [
            Ent("filme", "Duna: Parte Dois", 2024, 12, 4.5, criadores: "Denis Villeneuve"),
            Ent("filme", "Oppenheimer", 2023, 60, 4, criadores: "Christopher Nolan"),
            Ent("serie", "Ruptura", 2022, 30, 5, criadores: "Dan Erickson"),
            Ent("animacao", "Divertida Mente 2", 2024, 90, 3.5, criadores: "Kelsey Mann"),
            Ent("anime", "Frieren", 2023, null, null, "andamento", "Madhouse"),
            Ent("jogo", "Hollow Knight", 2017, 120, 5, criadores: "Team Cherry"),
            Ent("jogo", "Elden Ring", 2022, null, null, "quero", "FromSoftware"),
            Ent("livro", "Projeto Hail Mary", 2021, 45, 4.5, criadores: "Andy Weir"),
        ];

        var trabalho = TarefasCalculos.NovoQuadro("Trabalho", "💼");
        var faculdade = TarefasCalculos.NovoQuadro("Faculdade", "🎓");
        var setup = TarefasCalculos.NovoQuadro("Setup", "🖥️");
        s.Tarefas = new DadosTarefas { Quadros = [trabalho, faculdade, setup], Ativo = trabalho.Id };
        var ordem = 0;
        CardTarefa Card(Quadro q, int coluna, string titulo, int? prazo = null, string prioridade = "media", int? concluido = null,
            string[]? etiquetas = null, (string Texto, bool Feito)[]? checklist = null, bool foco = false) => new()
        {
            Id = NovoId(), QuadroId = q.Id, ColunaId = q.Colunas[coluna].Id, Titulo = titulo, Ordem = ordem++,
            Prazo = prazo is int p ? hoje.AddDays(p) : null, Prioridade = prioridade, Foco = foco, Criado = Dia(10),
            Concluido = concluido is int c ? Dia(c) : null, Etiquetas = [.. etiquetas ?? []],
            Checklist = [.. (checklist ?? []).Select(i => new ItemChecklist { Texto = i.Texto, Feito = i.Feito })],
        };
        s.Tarefas.Cards =
        [
            Card(trabalho, 0, "Enviar relatório mensal", 0, "alta", etiquetas: ["relatório"]),
            Card(trabalho, 0, "Revisar proposta do cliente", 3, etiquetas: ["cliente"]),
            Card(trabalho, 0, "Responder e-mails pendentes", -1, "baixa"),
            Card(trabalho, 1, "Apresentação da reunião de sexta", 2, "alta", checklist: [("Levantar números", true), ("Montar slides", false), ("Ensaiar", false)]),
            Card(trabalho, 2, "Atualizar planilha de custos", prioridade: "baixa", concluido: 0),
            Card(faculdade, 0, "Lista de exercícios de cálculo", 5, etiquetas: ["cálculo"]),
            Card(faculdade, 1, "Trabalho em grupo de engenharia de software", 9, "alta", checklist: [("Dividir tarefas", true), ("Escrever introdução", false)]),
            Card(faculdade, 2, "Prova de estatística", prioridade: "alta", concluido: 6),
            Card(setup, 0, "Comprar monitor 27\"", etiquetas: ["compra"]),
            Card(setup, 0, "Organizar cabos da mesa", prioridade: "baixa", foco: true, checklist: [("Comprar velcro", false), ("Suporte de cabos", false)]),
            Card(setup, 2, "Cadeira ergonômica", concluido: 20, etiquetas: ["compra"]),
        ];
        return s;
    }
}
