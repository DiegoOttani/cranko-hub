namespace CrankoHub.Services;

/// <summary>Permite que partes fora do Blazor (ex.: clique numa notificação) abram uma página.</summary>
public class NavegacaoService
{
    public event Action<string>? AoNavegar;

    /// <summary>Abre a página (ex.: "carro?aba=documentos"; "" = Início).</summary>
    public void Navegar(string link) => AoNavegar?.Invoke(link);
}
