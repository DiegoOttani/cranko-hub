namespace CrankoHub.Models;

/// <summary>Um serviço feito no veículo (troca de óleo, revisão, pneus...).</summary>
public class Manutencao
{
    public string Id { get; set; } = "";

    public string VeiculoId { get; set; } = "";

    /// <summary>Chave do tipo ("oleo", "revisao"...). Ver <see cref="TipoManutencao"/>.</summary>
    public string Tipo { get; set; } = "oleo";

    public DateOnly Data { get; set; }

    /// <summary>Quilometragem do veículo no dia do serviço.</summary>
    public double? Odometro { get; set; }

    /// <summary>Valor total pago (R$).</summary>
    public double? Valor { get; set; }

    /// <summary>O que foi feito/usado (ex.: "Óleo 5W30 sintético + filtro").</summary>
    public string? Descricao { get; set; }

    /// <summary>Oficina ou loja.</summary>
    public string? Local { get; set; }

    public string? Obs { get; set; }

    /// <summary>Fazer de novo depois de quantos km (lembrete).</summary>
    public double? IntervaloKm { get; set; }

    /// <summary>Fazer de novo depois de quantos meses (lembrete).</summary>
    public int? IntervaloMeses { get; set; }
}
