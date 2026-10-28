using System.Diagnostics;

namespace CrankoHub.Linux;

/// <summary>Notificações da área de trabalho pelo notify-send (vem instalado no Linux Mint).</summary>
public static class Notificacao
{
    public static void Enviar(string titulo, string texto)
    {
        try
        {
            var info = new ProcessStartInfo("notify-send") { UseShellExecute = false };
            info.ArgumentList.Add("--app-name=CrankoHub");
            var icone = Path.Combine(AppContext.BaseDirectory, "icone.png");
            if (File.Exists(icone)) info.ArgumentList.Add($"--icon={icone}");
            info.ArgumentList.Add(titulo);
            info.ArgumentList.Add(texto);
            Process.Start(info)?.Dispose();
        }
        catch { } // sem notify-send: segue sem notificação
    }
}
