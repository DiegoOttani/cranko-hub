namespace CrankoHub.Services;

/// <summary>
/// Versão Linux de "iniciar com o sistema": um atalho em ~/.config/autostart (padrão do Cinnamon).
/// O nome da classe é o mesmo da versão Windows para a página Configurações compilar sem mudança.
/// </summary>
public static class InicializacaoWindows
{
    /// <summary>Sem bandeja no Linux, o app sempre abre com a janela; mantido por compatibilidade.</summary>
    public const string ArgMinimizado = "--minimizado";

    private static readonly string Arquivo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", "crankohub.desktop");

    public static void Aplicar(bool ligado)
    {
        if (!ligado || Environment.ProcessPath is not string exe)
        {
            File.Delete(Arquivo);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
        File.WriteAllText(Arquivo, $"""
            [Desktop Entry]
            Type=Application
            Name=CrankoHub
            Comment=Painel pessoal de parâmetros
            Exec="{exe}"
            Icon={Path.Combine(AppContext.BaseDirectory, "icone.png")}
            Terminal=false
            X-GNOME-Autostart-enabled=true

            """);
    }
}
