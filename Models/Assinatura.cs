namespace CrankoHub.Models;

/// <summary>Uma assinatura/serviço recorrente (streaming, software, academia...).</summary>
public class Assinatura
{
    public string Id { get; set; } = "";

    public string Nome { get; set; } = "";

    /// <summary>Valor cobrado a cada ciclo (R$).</summary>
    public double Valor { get; set; }

    /// <summary>Chave do ciclo ("mensal", "anual"...). Ver <see cref="CicloCobranca"/>.</summary>
    public string Ciclo { get; set; } = "mensal";

    /// <summary>
    /// Data de uma cobrança conhecida. A próxima cobrança é calculada a partir dela,
    /// avançando pelo ciclo, então não é preciso atualizar a data todo mês.
    /// </summary>
    public DateOnly Vencimento { get; set; }

    public string? Categoria { get; set; }

    /// <summary>Cartão, Pix, boleto...</summary>
    public string? Pagamento { get; set; }

    /// <summary>Assinaturas canceladas ficam no histórico, mas fora dos totais.</summary>
    public bool Ativa { get; set; } = true;

    public string? Obs { get; set; }
}
