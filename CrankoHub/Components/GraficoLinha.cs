using CrankoHub.Models;
using Microsoft.AspNetCore.Components;

namespace CrankoHub.Components;

/// <summary>Gráfico de linhas no tempo, com tooltip e linhas de referência (meta, média).</summary>
public class GraficoLinha : GraficoBase
{
    [Parameter] public IReadOnlyList<SerieLinha> Series { get; set; } = [];

    [Parameter] public IReadOnlyList<ReferenciaLinha> Referencias { get; set; } = [];

    /// <summary>Formato dos valores no tooltip.</summary>
    [Parameter] public FormatoNumero? Formato { get; set; }

    /// <summary>Formato dos rótulos do eixo Y.</summary>
    [Parameter] public FormatoNumero? FormatoY { get; set; }

    protected override string FuncaoJs => "crankoCharts.line";

    protected override object Opcoes() => new
    {
        series = Series,
        refs = Referencias,
        height = Altura,
        fmt = Formato,
        yFmt = FormatoY,
        emptyMsg = MsgVazia,
        label = Rotulo,
    };
}
