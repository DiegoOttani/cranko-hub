using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace CrankoHub.Linux;

/// <summary>Arquivos que a janela enxerga: o wwwroot e, em "capas/", a pasta das capas do usuário.</summary>
public sealed class ArquivosDoApp(IFileProvider wwwroot, IFileProvider capas) : IFileProvider
{
    private const string Prefixo = "capas/";

    public IFileInfo GetFileInfo(string subpath)
    {
        var caminho = subpath.TrimStart('/');
        return caminho.StartsWith(Prefixo, StringComparison.OrdinalIgnoreCase)
            ? capas.GetFileInfo(caminho[Prefixo.Length..])
            : wwwroot.GetFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => wwwroot.GetDirectoryContents(subpath);

    public IChangeToken Watch(string filter) => wwwroot.Watch(filter);
}
