namespace CrankoHub.Models;

/// <summary>Quadros de tarefas (estilo Kanban) e os cards de todos eles.</summary>
public class DadosTarefas
{
    public List<Quadro> Quadros { get; set; } = [];

    /// <summary>Id do quadro aberto, ou <see cref="Quadro.Hoje"/> para a visão do dia.</summary>
    public string Ativo { get; set; } = "";

    public List<CardTarefa> Cards { get; set; } = [];
}

/// <summary>Um quadro (Trabalho, Faculdade, Pessoal...) com suas colunas.</summary>
public class Quadro
{
    /// <summary>Id especial da visão "Hoje", que junta os cards de todos os quadros.</summary>
    public const string Hoje = "hoje";

    public string Id { get; set; } = "";

    public string Nome { get; set; } = "";

    public string? Emoji { get; set; }

    /// <summary>A última coluna é a de concluídos.</summary>
    public List<ColunaQuadro> Colunas { get; set; } = [];
}

public class ColunaQuadro
{
    public string Id { get; set; } = "";

    public string Nome { get; set; } = "";
}

public class CardTarefa
{
    public string Id { get; set; } = "";

    public string QuadroId { get; set; } = "";

    public string ColunaId { get; set; } = "";

    public string Titulo { get; set; } = "";

    public string? Descricao { get; set; }

    public DateOnly? Prazo { get; set; }

    /// <summary>"baixa", "media" ou "alta". Ver <see cref="PrioridadeTarefa"/>.</summary>
    public string Prioridade { get; set; } = "media";

    public List<string> Etiquetas { get; set; } = [];

    public List<ItemChecklist> Checklist { get; set; } = [];

    /// <summary>Marcado para aparecer em "Hoje" mesmo sem prazo.</summary>
    public bool Foco { get; set; }

    /// <summary>Posição dentro da coluna (menor primeiro).</summary>
    public double Ordem { get; set; }

    public DateOnly Criado { get; set; }

    /// <summary>Quando entrou na coluna de concluídos (null se não está lá).</summary>
    public DateOnly? Concluido { get; set; }
}

public class ItemChecklist
{
    public string Texto { get; set; } = "";

    public bool Feito { get; set; }
}

/// <param name="Classe">Cor do badge (mesmas classes de <see cref="Services.Alerta"/>).</param>
public record PrioridadeTarefa(string Chave, string Nome, string Classe)
{
    public static readonly IReadOnlyList<PrioridadeTarefa> Todas =
    [
        new("alta", "Alta", "critical"),
        new("media", "Média", "warning"),
        new("baixa", "Baixa", ""),
    ];

    public static PrioridadeTarefa Buscar(string chave) => Todas.FirstOrDefault(p => p.Chave == chave) ?? Todas[1];
}
