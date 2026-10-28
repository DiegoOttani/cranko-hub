using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Regras dos quadros de tarefas: colunas, conclusão, visão do dia e resumo.</summary>
public static class TarefasCalculos
{
    /// <summary>Cards concluídos há mais tempo que isso ficam escondidos (se o usuário quiser).</summary>
    public const int DiasConcluidosVisiveis = 14;

    public record Resumo(List<CardTarefa> ConcluidosHoje, List<CardTarefa> ParaHoje, List<CardTarefa> Atrasados);

    public static Quadro NovoQuadro(string nome, string? emoji = null) => new()
    {
        Id = ArmazenamentoService.NovoId(),
        Nome = nome,
        Emoji = emoji,
        Colunas =
        [
            new() { Id = ArmazenamentoService.NovoId(), Nome = "A fazer" },
            new() { Id = ArmazenamentoService.NovoId(), Nome = "Fazendo" },
            new() { Id = ArmazenamentoService.NovoId(), Nome = "Feito" },
        ],
    };

    public static Quadro? QuadroDe(DadosTarefas t, CardTarefa c) => t.Quadros.FirstOrDefault(q => q.Id == c.QuadroId);

    public static bool EhConcluida(Quadro q, string colunaId) => q.Colunas.Count > 0 && q.Colunas[^1].Id == colunaId;

    public static bool Aberto(CardTarefa c) => c.Concluido is null;

    /// <summary>Cards de uma coluna, na ordem.</summary>
    public static List<CardTarefa> DaColuna(DadosTarefas t, string colunaId) =>
        t.Cards.Where(c => c.ColunaId == colunaId).OrderBy(c => c.Ordem).ToList();

    /// <summary>Coloca o card na coluna, na posição indicada (null = no fim), e atualiza a data de conclusão.</summary>
    public static void Mover(DadosTarefas t, CardTarefa card, string colunaId, int? posicao, DateOnly hoje)
    {
        var q = QuadroDe(t, card);
        if (q is null || q.Colunas.All(c => c.Id != colunaId)) return;
        var destino = DaColuna(t, colunaId).Where(c => c != card).ToList();
        destino.Insert(Math.Clamp(posicao ?? destino.Count, 0, destino.Count), card);
        for (var i = 0; i < destino.Count; i++) destino[i].Ordem = i;

        card.ColunaId = colunaId;
        if (EhConcluida(q, colunaId)) card.Concluido ??= hoje;
        else card.Concluido = null;
    }

    /// <summary>Marca como feito (última coluna) ou devolve para a primeira.</summary>
    public static void AlternarConcluida(DadosTarefas t, CardTarefa card, DateOnly hoje)
    {
        if (QuadroDe(t, card) is not { Colunas.Count: > 0 } q) return;
        Mover(t, card, card.Concluido is null ? q.Colunas[^1].Id : q.Colunas[0].Id, null, hoje);
    }

    public static bool Atrasada(CardTarefa c, DateOnly hoje) => Aberto(c) && c.Prazo < hoje;

    /// <summary>Cards abertos que pedem atenção hoje (prazo até hoje ou em foco) mais os concluídos hoje.</summary>
    public static List<CardTarefa> DeHoje(DadosTarefas t, DateOnly hoje) =>
        t.Cards.Where(c => Aberto(c) ? c.Foco || c.Prazo <= hoje : c.Concluido == hoje)
            .OrderBy(c => c.Concluido is not null)
            .ThenBy(c => c.Prazo ?? DateOnly.MaxValue)
            .ThenBy(c => PrioridadeIndice(c.Prioridade))
            .ToList();

    public static Resumo ResumoDoDia(DadosTarefas t, DateOnly hoje) => new(
        t.Cards.Where(c => c.Concluido == hoje).ToList(),
        t.Cards.Where(c => Aberto(c) && (c.Prazo == hoje || (c.Foco && !(c.Prazo < hoje)))).ToList(),
        t.Cards.Where(c => Atrasada(c, hoje)).OrderBy(c => c.Prazo).ToList());

    public static int PrioridadeIndice(string chave)
    {
        for (var i = 0; i < PrioridadeTarefa.Todas.Count; i++)
            if (PrioridadeTarefa.Todas[i].Chave == chave) return i;
        return 1;
    }

    /// <summary>Texto e cor do badge do prazo.</summary>
    public static (string Texto, string Classe) Prazo(DateOnly prazo, DateOnly hoje)
    {
        var dias = prazo.DayNumber - hoje.DayNumber;
        return dias switch
        {
            < -1 => ($"atrasada {-dias} dias", "critical"),
            -1 => ("venceu ontem", "critical"),
            0 => ("hoje", "serious"),
            1 => ("amanhã", "warning"),
            < 7 => ($"em {dias} dias", ""),
            _ => (Formatacao.DataCurta(prazo), ""),
        };
    }

    public static string NomeQuadro(Quadro q) => string.IsNullOrEmpty(q.Emoji) ? q.Nome : $"{q.Emoji} {q.Nome}";
}
