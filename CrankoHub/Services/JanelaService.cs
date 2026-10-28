namespace CrankoHub.Services;

/// <summary>Avisa a janela nativa (barra de título) quando o tema do app muda.</summary>
public class JanelaService
{
    /// <summary>Novo tema: "light", "dark" ou null (segue o Windows).</summary>
    public event Action<string?>? AoMudarTema;

    public void TemaMudou(string? tema) => AoMudarTema?.Invoke(tema);
}
