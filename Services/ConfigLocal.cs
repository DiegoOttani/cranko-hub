using System.Text.Json;

namespace CrankoHub.Services;

/// <summary>
/// Ajustes só deste computador, em %LocalAppData%\CrankoHub\local.json: onde ficam os dados
/// (que podem estar numa pasta sincronizada) e o que não deve ir para o outro PC.
/// </summary>
public class ConfigLocal
{
    public static readonly string Pasta = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrankoHub");

    private static readonly string Caminho = Path.Combine(Pasta, "local.json");

    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static ConfigLocal Atual { get; } = Carregar();

    /// <summary>Pasta dos dados (OneDrive, Google Drive...). null = a pasta local de sempre.</summary>
    public string? PastaDados { get; set; }

    /// <summary>null até migrar a opção antiga, que ficava nos dados (e iria junto para o outro PC).</summary>
    public bool? IniciarComWindows { get; set; }

    public void Salvar()
    {
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Caminho, JsonSerializer.Serialize(this, Opcoes));
    }

    private static ConfigLocal Carregar()
    {
        try
        {
            if (File.Exists(Caminho) && JsonSerializer.Deserialize<ConfigLocal>(File.ReadAllText(Caminho), Opcoes) is { } c)
                return c;
        }
        catch { }
        return new ConfigLocal();
    }
}
