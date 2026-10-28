using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Contas dos Documentos do carro: o que é de cada veículo e o que está vencendo.</summary>
public static class DocumentosCalculos
{
    /// <summary>Considera "vencendo" o que vence nestes próximos dias.</summary>
    public const int DiasAviso = 30;

    /// <summary>Documentos de um veículo mais os do motorista, por vencimento.</summary>
    public static List<DocumentoCarro> DoVeiculo(DadosCarro carro, string veiculoId) =>
        carro.Documentos
            .Where(x => x.VeiculoId == veiculoId || x.VeiculoId is null)
            .OrderBy(x => x.Vencimento)
            .ToList();

    /// <summary>Não pagos, do vencimento mais próximo para o mais distante.</summary>
    public static List<DocumentoCarro> Pendentes(IEnumerable<DocumentoCarro> lista) =>
        lista.Where(x => !x.Pago).OrderBy(x => x.Vencimento).ToList();

    /// <summary>Vencidos ou vencendo nos próximos <see cref="DiasAviso"/> dias.</summary>
    public static bool PrecisaAtencao(DocumentoCarro x, DateOnly hoje) =>
        !x.Pago && x.Vencimento.DayNumber - hoje.DayNumber <= DiasAviso;

    public static (string Texto, string Classe) Situacao(DocumentoCarro x, DateOnly hoje)
    {
        if (x.Pago) return (TipoDocumento.Buscar(x.Tipo).AcaoPago.ToLowerInvariant(), "good");
        var dias = x.Vencimento.DayNumber - hoje.DayNumber;
        return dias switch
        {
            < 0 => ($"vencido há {-dias} {Formatacao.Plural(-dias, "dia", "dias")}", "critical"),
            0 => ("vence hoje", "critical"),
            <= 7 => ($"vence em {dias} {Formatacao.Plural(dias, "dia", "dias")}", "serious"),
            <= DiasAviso => ($"vence em {dias} dias", "warning"),
            _ => ($"em {dias} dias", ""),
        };
    }
}
