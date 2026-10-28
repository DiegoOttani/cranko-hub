using System.Globalization;

namespace CrankoHub.Services;

/// <summary>Formatação e leitura de números e datas no padrão brasileiro.</summary>
public static class Formatacao
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly string[] Meses = ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    public const string Vazio = "—";

    public static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Now);

    // ---- Números para exibição ----

    public static string Num(double? v, int casas = 1)
    {
        if (v is not double x || !double.IsFinite(x)) return Vazio;
        x = Math.Round(x, casas);
        if (x == 0) x = 0; // evita "-0,0"
        return x.ToString("N" + casas, PtBr);
    }

    public static string Money(double? v, int casas = 2)
    {
        if (v is not double x || !double.IsFinite(x)) return Vazio;
        return x.ToString("C" + casas, PtBr);
    }

    /// <summary>Número com sinal "+" quando positivo (ex.: "+1,2").</summary>
    public static string ComSinal(double v, int casas = 1) => (v > 0 ? "+" : "") + Num(v, casas);

    /// <summary>Valor para colocar dentro de um campo de texto (sem separador de milhar).</summary>
    public static string InVal(double? v, int casas = 2)
    {
        if (v is not double x || !double.IsFinite(x)) return "";
        return Math.Round(x, casas).ToString("0.##########", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    /// <summary>Lê número digitado aceitando "82,5", "82.5" ou "1.234,5".</summary>
    public static double? Pf(string? v)
    {
        if (v is null) return null;
        var s = string.Concat(v.Where(c => !char.IsWhiteSpace(c)));
        if (s.Length == 0) return null;
        if (s.Contains(',')) s = s.Replace(".", "").Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
    }

    // ---- Datas ----

    public static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? LerIso(string? s) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static string DataBR(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>"05 jan"</summary>
    public static string DataCurta(DateOnly d) => $"{d.Day:00} {Meses[d.Month - 1]}";

    /// <summary>"05 jan 27"</summary>
    public static string DataCurtaAno(DateOnly d) => $"{d.Day:00} {Meses[d.Month - 1]} {d.Year % 100:00}";

    /// <summary>"jan/26"</summary>
    public static string MesLabel(int ano, int mes) => $"{Meses[mes - 1]}/{ano % 100:00}";

    /// <summary>"segunda-feira, 26 de outubro"</summary>
    public static string DataPorExtenso(DateTime d) => d.ToString("dddd, d 'de' MMMM", PtBr);

    /// <summary>Últimos n meses terminando no mês atual, do mais antigo para o mais novo.</summary>
    public static List<(int Ano, int Mes)> UltimosMeses(int n)
    {
        var hoje = DateTime.Today;
        var inicio = new DateTime(hoje.Year, hoje.Month, 1);
        return Enumerable.Range(0, n).Select(i => inicio.AddMonths(i - n + 1)).Select(d => (d.Year, d.Month)).ToList();
    }

    public static string Plural(int n, string singular, string plural) => n == 1 ? singular : plural;
}
