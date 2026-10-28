using Photino.NET;

namespace CrankoHub.Services;

/// <summary>Versão Linux: diálogos de arquivo/pasta do GTK, pela janela do Photino.</summary>
public class DialogoService : IDialogoService
{
    /// <summary>Janela principal (definida no Program.cs).</summary>
    public static PhotinoWindow? Janela { get; set; }

    private static readonly (string Name, string[] Extensions)[] FiltroJson = [("Backup do CrankoHub (*.json)", ["*.json"])];

    public event Func<string, Task<bool>>? AoConfirmar;

    public Task<bool> Confirmar(string mensagem) => AoConfirmar?.Invoke(mensagem) ?? Task.FromResult(false);

    public string? SalvarBackup(string nomeSugerido)
    {
        var pasta = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var caminho = Exibir(j => j.ShowSaveFile("Salvar backup", Path.Combine(pasta, nomeSugerido), FiltroJson));
        if (string.IsNullOrEmpty(caminho)) return null;
        return caminho.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? caminho : caminho + ".json";
    }

    public string? AbrirBackup() =>
        Exibir(j => j.ShowOpenFile("Escolha o backup para importar", null, false, FiltroJson).FirstOrDefault());

    public string? AbrirImagem() =>
        Exibir(j => j.ShowOpenFile("Escolha a imagem da capa", null, false,
            [("Imagens", ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.gif"])]).FirstOrDefault());

    public string? EscolherPasta(string titulo, string? pastaInicial) =>
        Exibir(j => j.ShowOpenFolder(titulo, pastaInicial is not null && Directory.Exists(pastaInicial) ? pastaInicial : null, false).FirstOrDefault());

    private static string? Exibir(Func<PhotinoWindow, string?> abrir)
    {
        if (Janela is null) return null;
        string? resultado = null;
        Janela.Invoke(() => resultado = abrir(Janela));
        return string.IsNullOrEmpty(resultado) ? null : resultado;
    }
}
