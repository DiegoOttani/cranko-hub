namespace CrankoHub.Models;

/// <summary>Um registro diário da dieta. Só o peso é obrigatório.</summary>
public class RegistroPeso
{
    public string Id { get; set; } = "";

    public DateOnly Data { get; set; }

    public double Peso { get; set; }

    /// <summary>% de gordura corporal.</summary>
    public double? Gordura { get; set; }

    /// <summary>Cintura em centímetros.</summary>
    public double? Cintura { get; set; }

    public string? Obs { get; set; }
}
