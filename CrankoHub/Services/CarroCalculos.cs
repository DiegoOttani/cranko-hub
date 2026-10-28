using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Resumo de uma lista de abastecimentos.</summary>
public record EstatisticasCarro(
    int N, double Km, double Litros, double Valor,
    double? Media, double? CustoKm, double? PrecoMedio, double? Melhor, double? Pior);

/// <summary>Contas do Carro: consumo, custos e agregados mensais.</summary>
public static class CarroCalculos
{
    /// <summary>Abastecimentos de um veículo, do mais antigo para o mais novo.</summary>
    public static List<Abastecimento> DoVeiculo(DadosCarro carro, string veiculoId) =>
        carro.Abastecimentos
            .Where(a => a.VeiculoId == veiculoId)
            .OrderBy(a => a.Data).ThenBy(a => a.Odometro ?? 0)
            .ToList();

    public static Veiculo VeiculoAtivo(DadosCarro carro) =>
        carro.Veiculos.FirstOrDefault(v => v.Id == carro.Ativo) ?? carro.Veiculos[0];

    public static EstatisticasCarro Estatisticas(IReadOnlyCollection<Abastecimento> lista)
    {
        var km = lista.Sum(a => a.Km);
        var litros = lista.Sum(a => a.Litros);
        // Custos só consideram os abastecimentos com valor informado
        var comValor = lista.Where(a => a.Valor is not null).ToList();
        var valor = comValor.Sum(a => a.Valor!.Value);
        var kmComValor = comValor.Sum(a => a.Km);
        var litrosComValor = comValor.Sum(a => a.Litros);
        return new EstatisticasCarro(
            lista.Count, km, litros, valor,
            Media: litros > 0 ? km / litros : null,
            CustoKm: kmComValor > 0 ? valor / kmComValor : null,
            PrecoMedio: litrosComValor > 0 ? valor / litrosComValor : null,
            Melhor: lista.Count > 0 ? lista.Max(a => a.Kml) : null,
            Pior: lista.Count > 0 ? lista.Min(a => a.Kml) : null);
    }

    /// <summary>Soma mensal dos últimos 12 meses.</summary>
    public static List<BarraDado> Mensal(IReadOnlyCollection<Abastecimento> lista, Func<Abastecimento, double> valor) =>
        Formatacao.UltimosMeses(12).Select(m =>
        {
            var rotulo = Formatacao.MesLabel(m.Ano, m.Mes);
            return new BarraDado(rotulo, lista.Where(a => a.Data.Year == m.Ano && a.Data.Month == m.Mes).Sum(valor), rotulo);
        }).ToList();

    public static double GastoNoMes(IEnumerable<Abastecimento> lista, DateOnly referencia) =>
        lista.Where(a => a.Data.Year == referencia.Year && a.Data.Month == referencia.Month).Sum(a => a.Valor ?? 0);

    /// <summary>Último preço por litro pago num combustível.</summary>
    public static double? UltimoPreco(IReadOnlyList<Abastecimento> lista, string combustivel)
    {
        var a = lista.LastOrDefault(a => a.Combustivel == combustivel && a.Valor > 0 && a.Litros > 0);
        return a is null ? null : a.Valor!.Value / a.Litros;
    }
}
