using CrankoHub.Models;
using Microsoft.AspNetCore.Components;

namespace CrankoHub.Components;

/// <summary>Gráfico de barras (valores positivos e negativos).</summary>
public class GraficoBarras : GraficoBase
{
    [Parameter] public IReadOnlyList<BarraDado> Dados { get; set; } = [];

    /// <summary>Nome da série exibido no tooltip.</summary>
    [Parameter] public string Nome { get; set; } = "";

    [Parameter] public string Cor { get; set; } = "var(--series-1)";

    [Parameter] public FormatoNumero? Formato { get; set; }

    [Parameter] public FormatoNumero? FormatoY { get; set; }

    protected override string FuncaoJs => "crankoCharts.bar";

    protected override object Opcoes() => new
    {
        data = Dados,
        name = Nome,
        height = Altura,
        color = Cor,
        fmt = Formato,
        yFmt = FormatoY,
        emptyMsg = MsgVazia,
        label = Rotulo,
    };
}
