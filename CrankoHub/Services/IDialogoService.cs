namespace CrankoHub.Services;

/// <summary>
/// Expõe para o Blazor as janelas nativas do Windows (confirmação, abrir/salvar arquivo).
/// </summary>
public interface IDialogoService
{
    /// <summary>
    /// Pergunta Sim/Não numa janela do próprio app (ver Components/Confirmacao.razor).
    /// A primeira linha é o título; o restante, a explicação. Retorna true se o usuário confirmar.
    /// </summary>
    Task<bool> Confirmar(string mensagem);

    /// <summary>Avisa o componente de confirmação que há uma pergunta para mostrar.</summary>
    event Func<string, Task<bool>>? AoConfirmar;

    /// <summary>Pergunta onde salvar o backup .json. Retorna null se cancelar.</summary>
    string? SalvarBackup(string nomeSugerido);

    /// <summary>Escolhe um backup .json para importar. Retorna null se cancelar.</summary>
    string? AbrirBackup();

    /// <summary>Escolhe uma imagem (capa). Retorna null se cancelar.</summary>
    string? AbrirImagem();

    /// <summary>Escolhe uma pasta. Retorna null se cancelar.</summary>
    string? EscolherPasta(string titulo, string? pastaInicial);
}
