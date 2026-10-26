namespace CrankoHub.Models;

/// <summary>Um vencimento do carro ou do motorista (IPVA, licenciamento, seguro, CNH).</summary>
public class DocumentoCarro
{
    public string Id { get; set; } = "";

    /// <summary>Veículo do documento; null para os do motorista (CNH), que valem para todos.</summary>
    public string? VeiculoId { get; set; }

    /// <summary>Chave do tipo ("ipva", "licenciamento"...). Ver <see cref="TipoDocumento"/>.</summary>
    public string Tipo { get; set; } = "ipva";

    /// <summary>Detalhe livre: "Parcela 2/3", seguradora, categoria da CNH...</summary>
    public string? Descricao { get; set; }

    public DateOnly Vencimento { get; set; }

    /// <summary>Valor a pagar (R$).</summary>
    public double? Valor { get; set; }

    /// <summary>Já pago (ou renovado): sai dos pendentes.</summary>
    public bool Pago { get; set; }

    public string? Obs { get; set; }
}
