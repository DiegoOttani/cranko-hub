using System.Windows;
using Forms = System.Windows.Forms;

namespace CrankoHub.Services;

/// <summary>Ícone do CrankoHub na bandeja do Windows (perto do relógio) e as notificações.</summary>
public sealed class BandejaService : IDisposable
{
    private readonly Forms.NotifyIcon _icone;
    private string? _linkDoBalao;

    /// <summary>Pedido para mostrar a janela; o texto é a página a abrir (ou null).</summary>
    public event Action<string?>? AoAbrir;
    public event Action? AoVerificarAvisos;
    public event Action? AoSair;

    public BandejaService()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir o CrankoHub", null, (_, _) => AoAbrir?.Invoke(null));
        menu.Items.Add("Verificar avisos agora", null, (_, _) => AoVerificarAvisos?.Invoke());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => AoSair?.Invoke());

        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icone.ico")).Stream;
        _icone = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream),
            Text = "CrankoHub",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icone.DoubleClick += (_, _) => AoAbrir?.Invoke(null);
        _icone.BalloonTipClicked += (_, _) => AoAbrir?.Invoke(_linkDoBalao);
    }

    /// <summary>Mostra uma notificação do Windows; clicar nela abre <paramref name="link"/>.</summary>
    public void Notificar(string titulo, string texto, string? link = null)
    {
        _linkDoBalao = link;
        _icone.ShowBalloonTip(10000, titulo, texto, Forms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        _icone.Visible = false;
        _icone.Dispose();
    }
}
