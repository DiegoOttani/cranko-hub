using System.Windows;
using CrankoHub.Services;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;

namespace CrankoHub;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        var servicos = new ServiceCollection();
        servicos.AddWpfBlazorWebView();
#if DEBUG
        servicos.AddBlazorWebViewDeveloperTools();
#endif

        // Serviços do aplicativo
        servicos.AddSingleton<IDialogoService, DialogoService>();
        servicos.AddSingleton<ToastService>();
        servicos.AddSingleton<ArmazenamentoService>();

        Resources.Add("services", servicos.BuildServiceProvider());

        InitializeComponent();
    }

    private void AoInicializarWebView(object? sender, BlazorWebViewInitializingEventArgs e)
    {
        // Guarda os dados do WebView2 no perfil do usuário, e não ao lado do .exe
        // (que pode estar numa pasta sem permissão de escrita, como Program Files).
        e.UserDataFolder = Path.Combine(ArmazenamentoService.PastaDados, "WebView2");
    }
}
