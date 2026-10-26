using System.Windows.Threading;

namespace CrankoHub.Services;

/// <summary>
/// De hora em hora (e logo depois de abrir) faz o backup do dia e manda notificações
/// com os alertas do Início, cada um no máximo uma vez por dia.
/// </summary>
public class AvisosService(ArmazenamentoService store, BackupService backup, BandejaService bandeja)
{
    private DispatcherTimer? _timer;

    public void Iniciar()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timer.Tick += (_, _) =>
        {
            _timer.Interval = TimeSpan.FromHours(1);
            Verificar();
        };
        _timer.Start();
    }

    /// <param name="manual">Pedido pelo usuário: avisa tudo de novo e diz quando não há nada.</param>
    public void Verificar(bool manual = false)
    {
        backup.ExecutarSeNecessario();
        var cfg = store.Dados.Config;
        if (!cfg.Avisos && !manual) return;

        var hoje = Formatacao.Hoje;
        var alertas = Alertas.Gerar(store.Dados, hoje)
            .Where(a => manual || !cfg.AvisosEnviados.TryGetValue(a.Chave, out var dia) || dia != hoje)
            .ToList();

        if (alertas.Count == 0)
        {
            if (manual) bandeja.Notificar("Tudo em dia", "Nada precisa da sua atenção agora.");
            return;
        }

        if (alertas.Count == 1)
            bandeja.Notificar(alertas[0].Titulo, alertas[0].Detalhe, alertas[0].Link);
        else
            bandeja.Notificar($"{alertas.Count} coisas precisam de atenção",
                string.Join("\n", alertas.Take(3).Select(a => "• " + a.Titulo)) + (alertas.Count > 3 ? "\n..." : ""), "");

        // Guarda o que já foi avisado hoje e esquece os dias anteriores
        foreach (var chave in cfg.AvisosEnviados.Where(p => p.Value != hoje).Select(p => p.Key).ToList())
            cfg.AvisosEnviados.Remove(chave);
        foreach (var a in alertas)
            cfg.AvisosEnviados[a.Chave] = hoje;
        store.Salvar();
    }
}
