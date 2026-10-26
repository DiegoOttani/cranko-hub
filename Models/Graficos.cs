namespace CrankoHub.Models;

// Estruturas enviadas ao charts.js (serializadas em camelCase).

/// <summary>Ponto de um gráfico de linha. X é a data; Note aparece no tooltip.</summary>
public record PontoLinha(DateOnly X, double Y, string? Note = null);

public record SerieLinha(string Name, string Color, IReadOnlyList<PontoLinha> Points, bool Markers = true, bool Area = false);

/// <summary>Linha de referência tracejada (ex.: meta, média).</summary>
public record ReferenciaLinha(double Y, string Label);

public record BarraDado(string Label, double Value, string? Tip = null);

/// <summary>
/// Como formatar um número no gráfico (funções não atravessam o C# → JS, então vai a "receita").
/// </summary>
public record FormatoNumero(int Casas = 1, string Sufixo = "", string Prefixo = "", bool Moeda = false, bool Sinal = false)
{
    public static FormatoNumero Kg => new(1, " kg");
    public static FormatoNumero Real => new(2, Moeda: true);
}

public record ItemLegenda(string Name, string Color);
