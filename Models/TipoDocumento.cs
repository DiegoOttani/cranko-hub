namespace CrankoHub.Models;

/// <summary>Tipos de documento com vencimento.</summary>
/// <param name="DoVeiculo">false para os do motorista (valem para todos os veículos).</param>
/// <param name="RenovaMeses">Se repete a cada tantos meses (sugere criar o próximo ao pagar).</param>
public record TipoDocumento(string Chave, string Nome, bool DoVeiculo, int? RenovaMeses, string AcaoPago)
{
    public static readonly IReadOnlyList<TipoDocumento> Todos =
    [
        new("ipva", "IPVA", true, 12, "Pago"),
        new("licenciamento", "Licenciamento", true, 12, "Pago"),
        new("seguro", "Seguro", true, 12, "Pago"),
        new("cnh", "CNH", false, null, "Renovada"),
    ];

    public static TipoDocumento Buscar(string chave) => Todos.FirstOrDefault(t => t.Chave == chave) ?? Todos[0];

    public static string NomeDe(string chave) => Buscar(chave).Nome;
}
