namespace CrankoHub.Services;

/// <summary>
/// Expõe para o Blazor as janelas nativas do Windows (confirmação, abrir/salvar arquivo).
/// </summary>
public interface IDialogoService
{
    /// <summary>Pergunta Sim/Não. Retorna true se o usuário confirmar.</summary>
    bool Confirmar(string mensagem);

    /// <summary>Pergunta onde salvar o backup .json. Retorna null se cancelar.</summary>
    string? SalvarBackup(string nomeSugerido);

    /// <summary>Escolhe um backup .json para importar. Retorna null se cancelar.</summary>
    string? AbrirBackup();
}
