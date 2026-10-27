namespace CrankoHub.Models;

/// <summary>Preferências do aplicativo (não fazem parte dos dados restaurados por um backup).</summary>
public class Configuracoes
{
    /// <summary>Faz uma cópia dos dados por dia, ao abrir o app.</summary>
    public bool BackupAutomatico { get; set; }

    public string? PastaBackup { get; set; }

    /// <summary>Quantos backups automáticos manter na pasta (os mais antigos são apagados).</summary>
    public int BackupsMantidos { get; set; } = 10;

    public DateOnly? UltimoBackup { get; set; }

    /// <summary>O X da janela esconde o app na bandeja (perto do relógio) em vez de fechar.</summary>
    /// <summary>Cor de destaque: chave da paleta (ver <see cref="Models.CorDestaque"/>) ou "#RRGGBB".</summary>
    public string CorDestaque { get; set; } = "azul";

    public bool MinimizarParaBandeja { get; set; } = true;

    /// <summary>Já avisou que o app continua rodando na bandeja.</summary>
    public bool DicaBandejaMostrada { get; set; }

    public bool IniciarComWindows { get; set; }

    /// <summary>Notificações do Windows com os alertas do Início.</summary>
    public bool Avisos { get; set; } = true;

    /// <summary>Chave do alerta → dia em que foi avisado (cada alerta é avisado uma vez por dia).</summary>
    public Dictionary<string, DateOnly> AvisosEnviados { get; set; } = [];

    /// <summary>Chave da API do TMDB (filmes, séries e animações).</summary>
    public string? ChaveTmdb { get; set; }

    /// <summary>Chave da API do RAWG (jogos).</summary>
    public string? ChaveRawg { get; set; }
}
