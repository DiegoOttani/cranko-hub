using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace CrankoHub.Components;

/// <summary>
/// Base dos gráficos: renderiza um &lt;div&gt; vazio e entrega o desenho ao charts.js.
/// Só redesenha quando os dados realmente mudam (digitar num formulário não refaz o gráfico).
/// </summary>
public abstract class GraficoBase : ComponentBase, IAsyncDisposable
{
    private ElementReference _elemento;
    private string? _ultimoDesenho;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public int Altura { get; set; } = 260;

    [Parameter] public string MsgVazia { get; set; } = "Sem dados ainda.";

    [Parameter] public string? Rotulo { get; set; }

    /// <summary>Função do window.crankoCharts que desenha este gráfico.</summary>
    protected abstract string FuncaoJs { get; }

    protected abstract object Opcoes();

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddElementReferenceCapture(1, e => _elemento = e);
        builder.CloseElement();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var opcoes = Opcoes();
        var assinatura = JsonSerializer.Serialize(opcoes);
        if (assinatura == _ultimoDesenho) return;
        _ultimoDesenho = assinatura;
        await JS.InvokeVoidAsync(FuncaoJs, _elemento, opcoes);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("crankoCharts.dispose", _elemento);
        }
        catch (JSDisconnectedException)
        {
            // A janela já foi fechada
        }
        GC.SuppressFinalize(this);
    }
}
