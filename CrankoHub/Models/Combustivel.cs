namespace CrankoHub.Models;

/// <summary>Combustíveis suportados. A cor segue o combustível, não a ordem das séries.</summary>
public record Combustivel(string Chave, string Nome, string Cor)
{
    public static readonly IReadOnlyList<Combustivel> Todos =
    [
        new("gasolina", "Gasolina", "var(--series-1)"),
        new("etanol", "Etanol", "var(--series-2)"),
        new("diesel", "Diesel", "var(--series-3)"),
    ];

    public static Combustivel? Buscar(string chave) => Todos.FirstOrDefault(c => c.Chave == chave);

    public static string NomeDe(string chave) => Buscar(chave)?.Nome ?? chave;

    public static string CorDe(string chave) => Buscar(chave)?.Cor ?? "var(--muted)";
}
