namespace CrankoHub.Services;

/// <summary>
/// Backup automático: uma cópia dos dados por dia numa pasta escolhida pelo usuário,
/// mantendo só os N mais recentes.
/// </summary>
public class BackupService(ArmazenamentoService store, ToastService toast)
{
    /// <summary>Prefixo dos arquivos; a limpeza só mexe em arquivos com este nome.</summary>
    public const string Prefixo = "crankohub-backup-";

    public static string PastaSugerida => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrankoHub", "Backups");

    /// <summary>Faz o backup do dia, se estiver ativado e ainda não tiver sido feito hoje.</summary>
    public void ExecutarSeNecessario()
    {
        var c = store.Dados.Config;
        if (c.BackupAutomatico && !string.IsNullOrWhiteSpace(c.PastaBackup) && c.UltimoBackup != Formatacao.Hoje)
            FazerAgora();
    }

    /// <summary>Grava o backup de hoje (substituindo o de hoje, se existir) e apaga os excedentes.</summary>
    public bool FazerAgora()
    {
        var c = store.Dados.Config;
        if (string.IsNullOrWhiteSpace(c.PastaBackup)) return false;
        try
        {
            Directory.CreateDirectory(c.PastaBackup);
            c.UltimoBackup = Formatacao.Hoje;
            var caminho = Path.Combine(c.PastaBackup, $"{Prefixo}{Formatacao.Iso(Formatacao.Hoje)}.json");
            File.WriteAllText(caminho, store.Exportar());
            store.Salvar();
            foreach (var antigo in Listar().Skip(Math.Max(1, c.BackupsMantidos)))
                antigo.Delete();
            return true;
        }
        catch (Exception ex)
        {
            toast.Erro($"Não foi possível fazer o backup automático: {ex.Message}");
            return false;
        }
    }

    /// <summary>Backups da pasta, do mais novo para o mais antigo.</summary>
    public List<FileInfo> Listar()
    {
        var pasta = store.Dados.Config.PastaBackup;
        if (string.IsNullOrWhiteSpace(pasta) || !Directory.Exists(pasta)) return [];
        return new DirectoryInfo(pasta).GetFiles(Prefixo + "*.json")
            .OrderByDescending(f => f.Name)
            .ToList();
    }
}
