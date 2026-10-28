using System.Net.Http;

namespace CrankoHub.Services;

/// <summary>
/// Capas da estante de Entretenimento, guardadas no PC para aparecerem sem internet.
/// O WebView enxerga a pasta pelo endereço <see cref="Host"/> (ver MainWindow).
/// </summary>
public class CapasService(HttpClient http)
{
    public static readonly string Pasta = Path.Combine(ArmazenamentoService.PastaDados, "capas");

    public const string Host = "capas.crankohub";

    /// <summary>Início do endereço das capas (a versão Linux serve a pasta em "capas/").</summary>
    public static string Prefixo { get; set; } = $"https://{Host}/";

    public static string? Url(string? arquivo) => string.IsNullOrEmpty(arquivo) ? null : Prefixo + arquivo;

    /// <summary>Baixa a imagem e devolve o nome do arquivo salvo (ou null se falhar).</summary>
    public async Task<string?> Baixar(string url)
    {
        try
        {
            var bytes = await http.GetByteArrayAsync(url);
            return Salvar(bytes, Path.GetExtension(new Uri(url).AbsolutePath));
        }
        catch { return null; }
    }

    /// <summary>Copia uma imagem escolhida no PC.</summary>
    public static string? Importar(string caminho)
    {
        try { return Salvar(File.ReadAllBytes(caminho), Path.GetExtension(caminho)); }
        catch { return null; }
    }

    public static void Excluir(string? arquivo)
    {
        if (string.IsNullOrEmpty(arquivo)) return;
        try { File.Delete(Path.Combine(Pasta, Path.GetFileName(arquivo))); } catch { }
    }

    private static string Salvar(byte[] bytes, string extensao)
    {
        Directory.CreateDirectory(Pasta);
        extensao = extensao.ToLowerInvariant();
        if (extensao is not (".png" or ".webp" or ".jpeg" or ".gif")) extensao = ".jpg";
        var nome = ArmazenamentoService.NovoId() + extensao;
        File.WriteAllBytes(Path.Combine(Pasta, nome), bytes);
        return nome;
    }
}
