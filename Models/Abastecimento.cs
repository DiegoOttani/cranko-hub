using System.Text.Json.Serialization;

namespace CrankoHub.Models;

public class Abastecimento
{
    public string Id { get; set; } = "";

    public string VeiculoId { get; set; } = "";

    public DateOnly Data { get; set; }

    /// <summary>Chave do combustível ("gasolina", "etanol", "diesel"). Ver <see cref="Combustivel"/>.</summary>
    public string Combustivel { get; set; } = "gasolina";

    public double? Odometro { get; set; }

    /// <summary>Km rodados desde o abastecimento anterior.</summary>
    public double Km { get; set; }

    public double Litros { get; set; }

    /// <summary>Valor total pago (R$).</summary>
    public double? Valor { get; set; }

    public string? Posto { get; set; }

    public string? Obs { get; set; }

    [JsonIgnore]
    public double Kml => Km / Litros;
}
