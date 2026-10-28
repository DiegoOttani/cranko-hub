using System.Net.Http;
using CrankoHub;
using CrankoHub.Linux;
using CrankoHub.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Photino.Blazor;

// Depois de uma atualização, espera a versão antiga fechar (ela ainda segura a trava abaixo)
var atualizado = AtualizacaoService.PrepararInicio(args);

// Uma instância só: se já estiver aberto, avisa e sai
Directory.CreateDirectory(ArmazenamentoService.PastaDados);
FileStream trava;
try { trava = new FileStream(Path.Combine(ArmazenamentoService.PastaDados, "instancia.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
catch (IOException)
{
    Notificacao.Enviar("CrankoHub", "O CrankoHub já está aberto.");
    return;
}

// wwwroot do app + pasta das capas do Entretenimento (servida em "capas/")
Directory.CreateDirectory(CapasService.Pasta);
CapasService.Prefixo = "capas/";
var arquivos = new ArquivosDoApp(
    new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "wwwroot")),
    new PhysicalFileProvider(CapasService.Pasta));

var builder = PhotinoBlazorAppBuilder.CreateDefault(arquivos, args);
var servicos = builder.Services;
servicos.AddSingleton<IDialogoService, DialogoService>();
servicos.AddSingleton<ToastService>();
servicos.AddSingleton<ArmazenamentoService>();
servicos.AddSingleton<BackupService>();
servicos.AddSingleton<NavegacaoService>();
servicos.AddSingleton<JanelaService>();
servicos.AddSingleton<BandejaService>();
servicos.AddSingleton<AvisosService>();
servicos.AddSingleton<AtualizacaoService>();
var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
// A Wikipedia/Wikidata bloqueia identificações genéricas: nome + endereço do projeto
http.DefaultRequestHeaders.UserAgent.ParseAdd(EntretenimentoBusca.UserAgent);
servicos.AddSingleton(http);
servicos.AddSingleton<CapasService>();
servicos.AddSingleton<EntretenimentoBusca>();
builder.RootComponents.Add<Main>("#app");

var app = builder.Build();
DialogoService.Janela = app.MainWindow;
app.MainWindow
    .SetTitle("CrankoHub")
    .SetSize(1280, 820)
    .SetMinSize(960, 600)
    .Center();
var icone = Path.Combine(AppContext.BaseDirectory, "icone.png");
if (File.Exists(icone)) app.MainWindow.SetIconFile(icone);

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    app.MainWindow.ShowMessage("CrankoHub", $"Ocorreu um erro inesperado:\n\n{(e.ExceptionObject as Exception)?.Message}");

var store = app.Services.GetRequiredService<ArmazenamentoService>();
// Atualiza o caminho do executável no autostart, caso ele tenha mudado de lugar
if (store.Dados.Config.IniciarComWindows)
    try { InicializacaoWindows.Aplicar(true); } catch { }
app.Services.GetRequiredService<AvisosService>().Iniciar();
app.Services.GetRequiredService<AtualizacaoService>().Iniciar();
if (atualizado)
    Notificacao.Enviar("CrankoHub atualizado", $"Agora na versão {AtualizacaoService.Texto(AtualizacaoService.VersaoAtual)}.");

app.Run();
trava.Dispose();
