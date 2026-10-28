namespace CrankoHub.Models;

/// <summary>Tipos de manutenção, com os intervalos sugeridos para o lembrete.</summary>
public record TipoManutencao(string Chave, string Nome, double? Km, int? Meses)
{
    public static readonly IReadOnlyList<TipoManutencao> Todos =
    [
        new("oleo", "Troca de óleo", 10000, 12),
        new("filtros", "Filtros (ar, combustível, cabine)", 15000, 12),
        new("revisao", "Revisão", 10000, 12),
        new("pneus", "Pneus / rodízio", 10000, null),
        new("alinhamento", "Alinhamento e balanceamento", 10000, 12),
        new("freios", "Freios", 20000, null),
        new("bateria", "Bateria", null, 36),
        new("outro", "Outro serviço", null, null),
    ];

    public static TipoManutencao Buscar(string chave) => Todos.FirstOrDefault(t => t.Chave == chave) ?? Todos[^1];

    public static string NomeDe(string chave) => Buscar(chave).Nome;
}
