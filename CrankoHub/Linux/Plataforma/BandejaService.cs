using CrankoHub.Linux;

namespace CrankoHub.Services;

/// <summary>
/// Versão Linux: ainda sem ícone na bandeja (fechar a janela encerra o app); só as notificações.
/// Mantém a mesma API da versão Windows para as telas compilarem sem mudança.
/// </summary>
public sealed class BandejaService : IDisposable
{
#pragma warning disable CS0067 // eventos usados só pela bandeja do Windows
    public event Action<string?>? AoAbrir;
    public event Action? AoVerificarAvisos;
    public event Action? AoSair;
#pragma warning restore CS0067

    public void AplicarCor(string hex) { }

    public void Notificar(string titulo, string texto, string? link = null) => Notificacao.Enviar(titulo, texto);

    public void Dispose() { }
}
