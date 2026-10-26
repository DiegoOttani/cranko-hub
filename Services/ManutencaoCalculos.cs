using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Situação do lembrete de um tipo de manutenção, a partir do último serviço registrado.</summary>
public record StatusManutencao(
    TipoManutencao Tipo, Manutencao Ultima,
    double? ProximaKm, DateOnly? ProximaData,
    double? KmRestantes, int? DiasRestantes,
    DateOnly? Previsao, double? Progresso)
{
    /// <summary>Sem intervalo de km nem de meses: não há lembrete.</summary>
    public bool TemLembrete => Progresso is not null;

    public bool Vencida => Progresso >= 1;

    public bool EmBreve => !Vencida && (Progresso >= 0.85 || KmRestantes <= 1000 || DiasRestantes <= 30);

    public (string Texto, string Classe) Situacao =>
        !TemLembrete ? ("sem lembrete", "")
        : Vencida ? ("vencida", "critical")
        : EmBreve ? ("em breve", "warning")
        : ("em dia", "good");

    /// <summary>Ex.: "faltam 1.250 km ou 45 dias" / "passou 300 km".</summary>
    public string Restante
    {
        get
        {
            var partes = new List<string>();
            if (KmRestantes is double km)
                partes.Add(km >= 0 ? $"{Formatacao.Num(km, 0)} km" : $"passou {Formatacao.Num(-km, 0)} km");
            if (DiasRestantes is int dias)
                partes.Add(dias >= 0 ? $"{dias} {Formatacao.Plural(dias, "dia", "dias")}" : $"atrasou {-dias} {Formatacao.Plural(-dias, "dia", "dias")}");
            if (partes.Count == 0) return "";
            var vencido = KmRestantes < 0 || DiasRestantes < 0;
            return vencido ? string.Join(" · ", partes) : "faltam " + string.Join(" ou ", partes);
        }
    }
}

/// <summary>Contas da Manutenção: odômetro estimado, lembretes e gastos.</summary>
public static class ManutencaoCalculos
{
    /// <summary>Serviços de um veículo, do mais antigo para o mais novo.</summary>
    public static List<Manutencao> DoVeiculo(DadosCarro carro, string veiculoId) =>
        carro.Manutencoes
            .Where(m => m.VeiculoId == veiculoId)
            .OrderBy(m => m.Data).ThenBy(m => m.Odometro ?? 0)
            .ToList();

    /// <summary>
    /// Quilometragem atual estimada: o último odômetro informado num abastecimento somado aos km
    /// dos abastecimentos seguintes (ou o maior odômetro das manutenções, se for maior).
    /// </summary>
    public static double? OdometroAtual(IEnumerable<Abastecimento> abastecimentos, IEnumerable<Manutencao> manutencoes)
    {
        double? odo = null;
        foreach (var a in abastecimentos)
        {
            if (a.Odometro is not null) odo = a.Odometro;
            else if (odo is not null) odo += a.Km;
        }
        var maiorManutencao = manutencoes.Max(m => m.Odometro);
        return maiorManutencao > (odo ?? 0) ? maiorManutencao : odo;
    }

    /// <summary>Média de km rodados por dia (últimos 90 dias; se não houver, todo o histórico).</summary>
    public static double? KmPorDia(IReadOnlyList<Abastecimento> abastecimentos, DateOnly hoje)
    {
        var recentes = abastecimentos.Where(a => a.Data > hoje.AddDays(-90)).ToList();
        if (recentes.Count >= 2) return recentes.Sum(a => a.Km) / 90.0;
        if (abastecimentos.Count < 2) return null;
        var dias = abastecimentos[^1].Data.DayNumber - abastecimentos[0].Data.DayNumber;
        return dias > 0 ? abastecimentos.Sum(a => a.Km) / dias : null;
    }

    /// <summary>Situação de cada tipo já registrado, com o último serviço de cada um; mais urgentes primeiro.</summary>
    public static List<StatusManutencao> Lembretes(IEnumerable<Manutencao> manutencoes, double? odometroAtual, double? kmPorDia, DateOnly hoje) =>
        manutencoes
            .GroupBy(m => m.Tipo)
            .Select(g => Status(g.OrderBy(m => m.Data).ThenBy(m => m.Odometro ?? 0).Last(), odometroAtual, kmPorDia, hoje))
            .OrderByDescending(s => s.Progresso ?? -1)
            .ThenBy(s => s.Tipo.Nome)
            .ToList();

    public static StatusManutencao Status(Manutencao ultima, double? odometroAtual, double? kmPorDia, DateOnly hoje)
    {
        var tipo = TipoManutencao.Buscar(ultima.Tipo);

        double? proximaKm = ultima.Odometro is not null && ultima.IntervaloKm > 0 ? ultima.Odometro + ultima.IntervaloKm : null;
        DateOnly? proximaData = ultima.IntervaloMeses > 0 ? ultima.Data.AddMonths(ultima.IntervaloMeses.Value) : null;

        double? kmRestantes = proximaKm is not null && odometroAtual is not null ? proximaKm - odometroAtual : null;
        int? diasRestantes = proximaData is not null ? proximaData.Value.DayNumber - hoje.DayNumber : null;

        // Quanto do intervalo já foi usado (o que estiver mais perto de vencer)
        double? progresso = null;
        if (kmRestantes is not null)
            progresso = (odometroAtual!.Value - ultima.Odometro!.Value) / ultima.IntervaloKm!.Value;
        if (proximaData is not null)
        {
            var total = proximaData.Value.DayNumber - ultima.Data.DayNumber;
            var porData = (double)(hoje.DayNumber - ultima.Data.DayNumber) / total;
            progresso = Math.Max(progresso ?? 0, porData);
        }

        // Data prevista: a data limite ou quando os km devem ser atingidos no ritmo atual, o que vier antes
        DateOnly? previsao = proximaData;
        if (kmRestantes is not null && kmPorDia > 0)
        {
            var porKm = hoje.AddDays((int)Math.Round(Math.Max(kmRestantes.Value, 0) / kmPorDia.Value));
            if (previsao is null || porKm < previsao) previsao = porKm;
        }

        return new StatusManutencao(tipo, ultima, proximaKm, proximaData, kmRestantes, diasRestantes, previsao,
            progresso is null ? null : Math.Max(progresso.Value, 0));
    }

    /// <summary>Gasto mensal com manutenção nos últimos 12 meses.</summary>
    public static List<BarraDado> Mensal(IReadOnlyCollection<Manutencao> lista) =>
        Formatacao.UltimosMeses(12).Select(m =>
        {
            var rotulo = Formatacao.MesLabel(m.Ano, m.Mes);
            return new BarraDado(rotulo, lista.Where(x => x.Data.Year == m.Ano && x.Data.Month == m.Mes).Sum(x => x.Valor ?? 0), rotulo);
        }).ToList();
}
