namespace CrankoHub.Models;

/// <summary>
/// Tudo o que o aplicativo guarda. O formato JSON é o mesmo do antigo ClaudeParameters (web),
/// então backups exportados de lá podem ser importados aqui sem conversão.
/// </summary>
public class EstadoApp
{
    public int Versao { get; set; } = 1;

    /// <summary>"light", "dark" ou null (segue o tema do Windows).</summary>
    public string? Tema { get; set; }

    public DadosDieta Dieta { get; set; } = new();

    public DadosCarro Carro { get; set; } = new();

    public List<Assinatura> Assinaturas { get; set; } = [];

    public List<ItemEntretenimento> Entretenimento { get; set; } = [];

    public DadosTarefas Tarefas { get; set; } = new();

    public Configuracoes Config { get; set; } = new();
}

public class DadosDieta
{
    public PerfilDieta Perfil { get; set; } = new();

    public List<RegistroPeso> Registros { get; set; } = [];
}

public class PerfilDieta
{
    /// <summary>Altura em centímetros.</summary>
    public double? Altura { get; set; }

    /// <summary>Peso meta em kg.</summary>
    public double? Meta { get; set; }
}

public class DadosCarro
{
    public List<Veiculo> Veiculos { get; set; } = [];

    /// <summary>Id do veículo selecionado.</summary>
    public string Ativo { get; set; } = "";

    public List<Abastecimento> Abastecimentos { get; set; } = [];

    public List<Manutencao> Manutencoes { get; set; } = [];

    public List<DocumentoCarro> Documentos { get; set; } = [];
}
