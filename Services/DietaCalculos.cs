using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Contas da Dieta: IMC, tendência, variação semanal, média móvel e previsão da meta.</summary>
public static class DietaCalculos
{
    public static List<RegistroPeso> Ordenados(IEnumerable<RegistroPeso> registros) =>
        registros.OrderBy(r => r.Data).ToList();

    public static double Imc(double peso, double alturaCm) => peso / Math.Pow(alturaCm / 100, 2);

    /// <summary>Classificação do IMC: texto e classe CSS do selo.</summary>
    public static (string Rotulo, string Classe) ClasseImc(double imc) => imc switch
    {
        < 18.5 => ("Abaixo do peso", "warning"),
        < 25 => ("Peso normal", "good"),
        < 30 => ("Sobrepeso", "warning"),
        < 35 => ("Obesidade grau I", "serious"),
        < 40 => ("Obesidade grau II", "critical"),
        _ => ("Obesidade grau III", "critical"),
    };

    /// <summary>Tendência em kg/dia pela regressão linear dos últimos 30 dias.</summary>
    public static double? Tendencia(IReadOnlyList<RegistroPeso> rs)
    {
        if (rs.Count < 3) return null;
        var ultimo = rs[^1].Data.DayNumber;
        var pts = rs.Where(r => ultimo - r.Data.DayNumber <= 30).Select(r => (X: (double)r.Data.DayNumber, Y: r.Peso)).ToList();
        if (pts.Count < 3 || pts[^1].X - pts[0].X < 7) return null;
        return Regressao(pts)?.Inclinacao;
    }

    /// <summary>Regressão linear simples.</summary>
    public static (double Inclinacao, double Intercepto)? Regressao(IReadOnlyList<(double X, double Y)> pts)
    {
        var n = pts.Count;
        if (n < 2) return null;
        double mx = pts.Average(p => p.X), my = pts.Average(p => p.Y), num = 0, den = 0;
        foreach (var p in pts)
        {
            num += (p.X - mx) * (p.Y - my);
            den += (p.X - mx) * (p.X - mx);
        }
        if (den == 0) return null;
        var inclinacao = num / den;
        return (inclinacao, my - inclinacao * mx);
    }

    /// <summary>Variação de peso semana a semana (último registro de cada semana), últimas 12.</summary>
    public static List<BarraDado> Semanal(IReadOnlyList<RegistroPeso> rs)
    {
        var semanas = new SortedDictionary<DateOnly, double>();
        foreach (var r in rs)
        {
            var segunda = r.Data.AddDays(-(((int)r.Data.DayOfWeek + 6) % 7));
            semanas[segunda] = r.Peso;
        }
        var chaves = semanas.Keys.ToList();
        var saida = new List<BarraDado>();
        for (var i = 1; i < chaves.Count; i++)
            saida.Add(new BarraDado(Formatacao.DataCurta(chaves[i]), semanas[chaves[i]] - semanas[chaves[i - 1]], $"Semana de {Formatacao.DataBR(chaves[i])}"));
        return saida.TakeLast(12).ToList();
    }

    /// <summary>Média móvel dos últimos <paramref name="dias"/> dias.</summary>
    public static List<PontoLinha> MediaMovel(IReadOnlyList<RegistroPeso> rs, int dias = 7) =>
        rs.Select(r =>
        {
            var janela = rs.Where(o => o.Data <= r.Data && r.Data.DayNumber - o.Data.DayNumber < dias).ToList();
            return new PontoLinha(r.Data, janela.Average(o => o.Peso));
        }).ToList();
}
