using System.Windows;
using System.Windows.Threading;
using CrankoHub.Services;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace CrankoHub;

public partial class App : Application
{
    private const string NomeMutex = "CrankoHub-instancia-unica";
    private const string NomeSinal = "CrankoHub-mostrar-janela";

    private Mutex? _instancia;
    private MainWindow? _janela;

    /// <summary>Serviços compartilhados entre o Blazor e a parte nativa (bandeja, avisos).</summary>
    public static IServiceProvider Servicos { get; private set; } = default!;

    /// <summary>true quando o usuário pediu para sair (o X não deve só esconder a janela).</summary>
    public static bool Saindo { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += AoOcorrerErroNaoTratado;
        base.OnStartup(e);

        // Uma instância só: se já estiver aberto (talvez na bandeja), pede para ele aparecer e sai
        _instancia = new Mutex(true, NomeMutex, out var primeira);
        if (!primeira)
        {
            try { EventWaitHandle.OpenExisting(NomeSinal).Set(); } catch { }
            Shutdown();
            return;
        }
        var sinal = new EventWaitHandle(false, EventResetMode.AutoReset, NomeSinal);
        new Thread(() => { while (sinal.WaitOne()) Dispatcher.Invoke(() => MostrarJanela()); }) { IsBackground = true }.Start();

        Servicos = CriarServicos();
        var store = Servicos.GetRequiredService<ArmazenamentoService>();
        var bandeja = Servicos.GetRequiredService<BandejaService>();
        var avisos = Servicos.GetRequiredService<AvisosService>();
        bandeja.AoAbrir += MostrarJanela;
        bandeja.AoVerificarAvisos += () => avisos.Verificar(manual: true);
        bandeja.AoSair += Sair;

        // Atualiza o caminho do .exe no registro, caso ele tenha mudado de lugar
        if (store.Dados.Config.IniciarComWindows)
            try { InicializacaoWindows.Aplicar(true); } catch { }

        _janela = new MainWindow();
        if (!e.Args.Contains(InicializacaoWindows.ArgMinimizado))
            _janela.Show();
        avisos.Iniciar();
    }

    private static IServiceProvider CriarServicos()
    {
        var servicos = new ServiceCollection();
        servicos.AddWpfBlazorWebView();
#if DEBUG
        servicos.AddBlazorWebViewDeveloperTools();
#endif
        servicos.AddSingleton<IDialogoService, DialogoService>();
        servicos.AddSingleton<ToastService>();
        servicos.AddSingleton<ArmazenamentoService>();
        servicos.AddSingleton<BackupService>();
        servicos.AddSingleton<NavegacaoService>();
        servicos.AddSingleton<BandejaService>();
        servicos.AddSingleton<AvisosService>();
        return servicos.BuildServiceProvider();
    }

    /// <summary>Traz a janela de volta (da bandeja ou de trás de outras) e, se pedido, abre uma página.</summary>
    public void MostrarJanela(string? link = null)
    {
        if (_janela is null) return;
        _janela.Show();
        if (_janela.WindowState == WindowState.Minimized) _janela.WindowState = WindowState.Normal;
        _janela.Activate();
        if (link is not null) Servicos.GetRequiredService<NavegacaoService>().Navegar(link);
    }

    public void Sair()
    {
        Saindo = true;
        Servicos.GetRequiredService<BandejaService>().Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instancia?.Dispose();
        base.OnExit(e);
    }

    private void AoOcorrerErroNaoTratado(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ocorreu um erro inesperado:\n\n{e.Exception.Message}",
            "CrankoHub",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
