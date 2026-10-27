using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CrankoHub.Services;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace CrankoHub;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Resources.Add("services", App.Servicos);
        InitializeComponent();

        AplicarTema(App.Servicos.GetRequiredService<ArmazenamentoService>().Dados.Tema);
        App.Servicos.GetRequiredService<JanelaService>().AoMudarTema += tema => Dispatcher.Invoke(() => AplicarTema(tema));
        StateChanged += (_, _) => AjustarMaximizada();
        SourceInitialized += (_, _) => ArredondarCantos();
    }

    private void AoInicializarWebView(object? sender, BlazorWebViewInitializingEventArgs e)
    {
        // Guarda os dados do WebView2 no perfil do usuário, e não ao lado do .exe
        // (que pode estar numa pasta sem permissão de escrita, como Program Files).
        e.UserDataFolder = Path.Combine(ArmazenamentoService.PastaDados, "WebView2");
    }

    // As capas do Entretenimento ficam numa pasta do usuário; o WebView as enxerga por um endereço próprio
    private void AoWebViewPronto(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        Directory.CreateDirectory(CapasService.Pasta);
        e.WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(CapasService.Host, CapasService.Pasta,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
    }

    // ---- Barra de título estilo Mac ----

    private void AoFechar(object sender, RoutedEventArgs e) => Close();

    private void AoMinimizar(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void AoMaximizar(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // Sem a moldura do Windows, a janela maximizada passaria um pouco das bordas da tela
    private void AjustarMaximizada()
    {
        // Borda de redimensionamento + a borda "acolchoada" do Windows (4px)
        var b = SystemParameters.WindowResizeBorderThickness;
        raiz.Margin = WindowState == WindowState.Maximized
            ? new Thickness(b.Left + 4, b.Top + 4, b.Right + 4, b.Bottom + 4)
            : new Thickness(0);
    }

    /// <summary>Cores da barra de acordo com o tema do app ("light", "dark" ou null = tema do Windows).</summary>
    private void AplicarTema(string? tema)
    {
        var escuro = tema switch { "dark" => true, "light" => false, _ => WindowsEstaEscuro() };
        Color Cor(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        barraTitulo.Background = new SolidColorBrush(Cor(escuro ? "#2C2C2E" : "#FFFFFF"));
        barraTitulo.BorderBrush = new SolidColorBrush(Cor(escuro ? "#3D3D41" : "#E5E5EA"));
        tituloJanela.Foreground = new SolidColorBrush(Cor(escuro ? "#D1D1D6" : "#48484A"));
        Resources["BolinhaInativa"] = new SolidColorBrush(Cor(escuro ? "#4A4A4A" : "#CFCFCF"));
        Background = new SolidColorBrush(Cor(escuro ? "#1C1C1E" : "#F5F5F7"));
    }

    private static bool WindowsEstaEscuro()
    {
        using var chave = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return chave?.GetValue("AppsUseLightTheme") is int claro && claro == 0;
    }

    // Mantém os cantos arredondados do Windows 11 (no Windows 10 a chamada só falha, sem efeito)
    private void ArredondarCantos()
    {
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;
        var preferencia = DWMWCP_ROUND;
        _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preferencia, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

    // ---- Fechar ----

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
