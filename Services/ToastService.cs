namespace CrankoHub.Services;

/// <summary>Mensagens rápidas no canto da tela ("Peso registrado!", erros de validação...).</summary>
public class ToastService
{
    public event Action<string, bool>? AoMostrar;

    public void Ok(string mensagem) => AoMostrar?.Invoke(mensagem, false);

    public void Erro(string mensagem) => AoMostrar?.Invoke(mensagem, true);
}
