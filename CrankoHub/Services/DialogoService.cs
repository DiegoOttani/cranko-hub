using System.Windows;
using Microsoft.Win32;

namespace CrankoHub.Services;

public class DialogoService : IDialogoService
{
    private const string FiltroJson = "Backup do CrankoHub (*.json)|*.json";

    public event Func<string, Task<bool>>? AoConfirmar;

    public Task<bool> Confirmar(string mensagem) => AoConfirmar?.Invoke(mensagem) ?? Task.FromResult(false);

    public string? SalvarBackup(string nomeSugerido)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Salvar backup",
            Filter = FiltroJson,
            FileName = nomeSugerido,
            DefaultExt = ".json",
            OverwritePrompt = true,
        };
        return Exibir(() => dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null);
    }

    public string? AbrirBackup()
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Escolha o backup para importar",
            Filter = FiltroJson,
            CheckFileExists = true,
        };
        return Exibir(() => dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null);
    }

    public string? AbrirImagem()
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Escolha a imagem da capa",
            Filter = "Imagens (*.jpg;*.jpeg;*.png;*.webp;*.gif)|*.jpg;*.jpeg;*.png;*.webp;*.gif",
            CheckFileExists = true,
        };
        return Exibir(() => dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FileName : null);
    }

    public string? EscolherPasta(string titulo, string? pastaInicial)
    {
        var dialogo = new OpenFolderDialog
        {
            Title = titulo,
            InitialDirectory = pastaInicial is not null && Directory.Exists(pastaInicial) ? pastaInicial : "",
        };
        return Exibir(() => dialogo.ShowDialog(Application.Current.MainWindow) == true ? dialogo.FolderName : null);
    }

    // Os diálogos do WPF precisam rodar na thread da interface.
    private static string? Exibir(Func<string?> abrir) =>
        Application.Current.Dispatcher.Invoke(abrir);
}
