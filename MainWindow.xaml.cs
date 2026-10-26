using System.ComponentModel;
using System.Windows;
using CrankoHub.Services;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;

namespace CrankoHub;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Resources.Add("services", App.Servicos);
        InitializeComponent();
    }

    private void AoInicializarWebView(object? sender, BlazorWebViewInitializingEventArgs e)
    {
        // Guarda os dados do WebView2 no perfil do usuário, e não ao lado do .exe
        // (que pode estar numa pasta sem permissão de escrita, como Program Files).
        e.UserDataFolder = Path.Combine(ArmazenamentoService.PastaDados, "WebView2");
    }

    // O X esconde na bandeja (o app continua avisando); "Sair" no menu do ícone fecha de verdade
    protected override void OnClosing(CancelEventArgs e)
    {
        var store = App.Servicos.GetRequiredService<ArmazenamentoService>();
        var cfg = store.Dados.Config;
        if (!App.Saindo && cfg.MinimizarParaBandeja)
        {
            e.Cancel = true;
            Hide();
            if (!cfg.DicaBandejaMostrada)
            {
                App.Servicos.GetRequiredService<BandejaService>().Notificar("O CrankoHub continua aberto",
                    "Ele fica perto do relógio para te avisar dos vencimentos. Para fechar de vez: botão direito no ícone > Sair.");
                cfg.DicaBandejaMostrada = true;
                store.Salvar();
            }
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!App.Saindo) ((App)Application.Current).Sair();
    }
}
