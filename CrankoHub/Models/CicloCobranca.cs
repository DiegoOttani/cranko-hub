namespace CrankoHub.Models;

/// <summary>De quanto em quanto tempo uma assinatura é cobrada.</summary>
public record CicloCobranca(string Chave, string Nome, int Meses, int Dias = 0)
{
    public static readonly IReadOnlyList<CicloCobranca> Todos =
    [
        new("semanal", "Semanal", 0, 7),
        new("mensal", "Mensal", 1),
        new("trimestral", "Trimestral", 3),
        new("semestral", "Semestral", 6),
        new("anual", "Anual", 12),
    ];

    public static CicloCobranca Buscar(string chave) => Todos.FirstOrDefault(c => c.Chave == chave) ?? Todos[1];

    /// <summary>Quanto um valor deste ciclo representa por mês.</summary>
    public double PorMes(double valor) => Dias > 0 ? valor * 365.0 / 12 / Dias : valor / Meses;

    /// <summary>A n-ésima cobrança contada a partir de <paramref name="inicio"/>.</summary>
    public DateOnly Avancar(DateOnly inicio, int n) => Dias > 0 ? inicio.AddDays(Dias * n) : inicio.AddMonths(Meses * n);
}
