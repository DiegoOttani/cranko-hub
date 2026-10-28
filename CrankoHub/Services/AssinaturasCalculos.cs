using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Contas das Assinaturas: próxima cobrança, custo mensal equivalente e totais.</summary>
public static class AssinaturasCalculos
{
    /// <summary>Sugestões para o campo de categoria.</summary>
    public static readonly IReadOnlyList<string> CategoriasSugeridas =
        ["Streaming", "Música", "Software", "Nuvem", "Jogos", "Notícias", "Academia", "Educação", "Telefonia", "Outros"];

    public const string SemCategoria = "Sem categoria";

    /// <summary>Primeira cobrança a partir de hoje (inclusive), avançando o vencimento pelo ciclo.</summary>
    public static DateOnly ProximaCobranca(Assinatura a, DateOnly hoje)
    {
        var ciclo = CicloCobranca.Buscar(a.Ciclo);
        if (a.Vencimento >= hoje) return a.Vencimento;
        // Sempre conta a partir da data original, para que o dia 31 não "escorregue" para 28
        var n = 1;
        while (ciclo.Avancar(a.Vencimento, n) < hoje) n++;
        return ciclo.Avancar(a.Vencimento, n);
    }

    public static double PorMes(Assinatura a) => CicloCobranca.Buscar(a.Ciclo).PorMes(a.Valor);

    public static string CategoriaDe(Assinatura a) => string.IsNullOrWhiteSpace(a.Categoria) ? SemCategoria : a.Categoria;

    /// <summary>Todas as cobranças das assinaturas ativas entre hoje e hoje + dias, em ordem.</summary>
    public static List<(Assinatura Assinatura, DateOnly Data)> CobrancasAte(IEnumerable<Assinatura> lista, DateOnly hoje, int dias)
    {
        var limite = hoje.AddDays(dias);
        var saida = new List<(Assinatura, DateOnly)>();
        foreach (var a in lista.Where(a => a.Ativa))
        {
            // Assinaturas semanais podem cobrar mais de uma vez no período
            for (var data = ProximaCobranca(a, hoje); data <= limite; data = ProximaCobranca(a, data.AddDays(1)))
                saida.Add((a, data));
        }
        return saida.OrderBy(x => x.Item2).ThenBy(x => x.Item1.Nome).ToList();
    }

    /// <summary>Custo mensal equivalente por categoria, do maior para o menor.</summary>
    public static List<BarraDado> PorCategoria(IEnumerable<Assinatura> lista) =>
        lista.Where(a => a.Ativa)
            .GroupBy(CategoriaDe)
            .Select(g => new BarraDado(g.Key, g.Sum(PorMes), $"{g.Key} · {g.Count()} {Formatacao.Plural(g.Count(), "assinatura", "assinaturas")}"))
            .OrderByDescending(b => b.Value)
            .ToList();

    /// <summary>Texto e classe CSS do selo "faltam X dias".</summary>
    public static (string Texto, string Classe) Prazo(int dias) => dias switch
    {
        0 => ("hoje", "critical"),
        1 => ("amanhã", "serious"),
        <= 3 => ($"em {dias} dias", "serious"),
        <= 7 => ($"em {dias} dias", "warning"),
        _ => ($"em {dias} dias", ""),
    };
}
