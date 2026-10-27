using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrankoHub.Models;

namespace CrankoHub.Services;

/// <summary>Um resultado de busca, já no formato para preencher um <see cref="ItemEntretenimento"/>.</summary>
public record ResultadoBusca(string Titulo, int? Ano, string? UrlCapa, string? Sinopse, string? Criadores, string? Generos, string Fonte, string FonteId);

/// <summary>Erro de busca com mensagem pronta para mostrar ao usuário.</summary>
public class BuscaException(string mensagem) : Exception(mensagem);

/// <summary>
/// Busca dados e capas nas APIs públicas: TMDB (filmes, séries, animações), RAWG (jogos),
/// AniList (animes) e Open Library (livros). TMDB e RAWG precisam de chave (nas Configurações).
/// </summary>
public partial class EntretenimentoBusca(HttpClient http, ArmazenamentoService store)
{
    private const int Maximo = 12;

    public async Task<List<ResultadoBusca>> Buscar(CategoriaEntretenimento cat, string termo)
    {
        termo = termo.Trim();
        if (termo.Length == 0) return [];
        try
        {
            return cat.Api switch
            {
                "tmdb-filme" => await Tmdb("movie", termo),
                "tmdb-serie" => await Tmdb("tv", termo),
                "rawg" => await Rawg(termo),
                "anilist" => await AniList(termo),
                _ => await OpenLibrary(termo),
            };
        }
        catch (BuscaException) { throw; }
        catch (HttpRequestException e) when (e.StatusCode is System.Net.HttpStatusCode.Unauthorized)
        {
            throw new BuscaException("A chave da API foi recusada. Confira nas Configurações.");
        }
        catch (Exception)
        {
            throw new BuscaException("Não foi possível buscar agora. Verifique a internet ou cadastre manualmente.");
        }
    }

    /// <summary>Completa o resultado escolhido com o que a busca não traz (diretor, estúdio, sinopse...).</summary>
    public async Task<ResultadoBusca> Detalhar(ResultadoBusca r)
    {
        try
        {
            switch (r.Fonte)
            {
                case "tmdb-movie" or "tmdb-tv":
                {
                    var tipo = r.Fonte[5..];
                    var j = await Get(UrlTmdb($"{tipo}/{r.FonteId}", "append_to_response=credits"), Bearer());
                    var generos = Juntar(j["genres"]?.AsArray().Select(g => Texto(g?["name"])));
                    var criadores = tipo == "movie"
                        ? Juntar(j["credits"]?["crew"]?.AsArray().Where(c => Texto(c?["job"]) == "Director").Select(c => Texto(c?["name"])))
                        : Juntar(j["created_by"]?.AsArray().Select(c => Texto(c?["name"])));
                    return r with { Generos = generos ?? r.Generos, Criadores = criadores ?? r.Criadores, Sinopse = Texto(j["overview"]) ?? r.Sinopse };
                }
                case "rawg":
                {
                    var j = await Get($"https://api.rawg.io/api/games/{r.FonteId}?key={Uri.EscapeDataString(ChaveRawg())}");
                    var devs = Juntar(j["developers"]?.AsArray().Select(d => Texto(d?["name"])));
                    return r with { Criadores = devs ?? r.Criadores, Sinopse = Texto(j["description_raw"]) ?? r.Sinopse };
                }
                case "openlibrary":
                {
                    var j = await Get($"https://openlibrary.org{r.FonteId}.json");
                    var desc = j["description"] is JsonObject o ? o["value"] : j["description"];
                    return r with { Sinopse = Texto(desc) ?? r.Sinopse };
                }
            }
        }
        catch { }
        return r;
    }

    // ---- APIs ----

    private async Task<List<ResultadoBusca>> Tmdb(string tipo, string termo)
    {
        var filme = tipo == "movie";
        var j = await Get(UrlTmdb($"search/{tipo}", "query=" + Uri.EscapeDataString(termo)), Bearer());
        return (j["results"]?.AsArray() ?? []).Take(Maximo).Select(x => new ResultadoBusca(
            Texto(x?[filme ? "title" : "name"]) ?? "",
            Ano(Texto(x?[filme ? "release_date" : "first_air_date"])),
            Texto(x?["poster_path"]) is string p ? "https://image.tmdb.org/t/p/w500" + p : null,
            Texto(x?["overview"]), null, null, "tmdb-" + tipo, x?["id"]?.ToString() ?? "")).ToList();
    }

    private async Task<List<ResultadoBusca>> Rawg(string termo)
    {
        var j = await Get($"https://api.rawg.io/api/games?page_size={Maximo}&search={Uri.EscapeDataString(termo)}&key={Uri.EscapeDataString(ChaveRawg())}");
        return (j["results"]?.AsArray() ?? []).Select(x => new ResultadoBusca(
            Texto(x?["name"]) ?? "", Ano(Texto(x?["released"])), Texto(x?["background_image"]), null, null,
            Juntar(x?["genres"]?.AsArray().Select(g => Texto(g?["name"]))), "rawg", x?["id"]?.ToString() ?? "")).ToList();
    }

    private async Task<List<ResultadoBusca>> AniList(string termo)
    {
        const string query = "query ($q: String) { Page(perPage: 12) { media(search: $q, type: ANIME) { id title { romaji english } startDate { year } coverImage { extraLarge } description(asHtml: false) genres studios(isMain: true) { nodes { name } } } } }";
        using var resp = await http.PostAsJsonAsync("https://graphql.anilist.co", new { query, variables = new { q = termo } });
        resp.EnsureSuccessStatusCode();
        var j = JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
        return (j["data"]?["Page"]?["media"]?.AsArray() ?? []).Select(x => new ResultadoBusca(
            Texto(x?["title"]?["english"]) ?? Texto(x?["title"]?["romaji"]) ?? "",
            x?["startDate"]?["year"]?.GetValueKind() == JsonValueKind.Number ? (int)x["startDate"]!["year"]! : null,
            Texto(x?["coverImage"]?["extraLarge"]),
            Texto(x?["description"]) is string d ? Tags().Replace(d, "").Trim() : null,
            Juntar(x?["studios"]?["nodes"]?.AsArray().Select(s => Texto(s?["name"]))),
            Juntar(x?["genres"]?.AsArray().Select(Texto)),
            "anilist", x?["id"]?.ToString() ?? "")).ToList();
    }

    private async Task<List<ResultadoBusca>> OpenLibrary(string termo)
    {
        var j = await Get($"https://openlibrary.org/search.json?limit={Maximo}&fields=key,title,author_name,first_publish_year,cover_i,subject&q={Uri.EscapeDataString(termo)}");
        return (j["docs"]?.AsArray() ?? []).Select(x => new ResultadoBusca(
            Texto(x?["title"]) ?? "",
            x?["first_publish_year"]?.GetValueKind() == JsonValueKind.Number ? (int)x["first_publish_year"]! : null,
            x?["cover_i"] is JsonNode c ? $"https://covers.openlibrary.org/b/id/{c}-L.jpg" : null, null,
            Juntar(x?["author_name"]?.AsArray().Take(3).Select(Texto)),
            Juntar(x?["subject"]?.AsArray().Take(3).Select(Texto)),
            "openlibrary", Texto(x?["key"]) ?? "")).ToList();
    }

    // ---- Auxiliares ----

    private string ChaveTmdb() => store.Dados.Config.ChaveTmdb?.Trim() is { Length: > 0 } k ? k
        : throw new BuscaException("Para buscar filmes, séries e animações, cole sua chave do TMDB nas Configurações.");

    private string ChaveRawg() => store.Dados.Config.ChaveRawg?.Trim() is { Length: > 0 } k ? k
        : throw new BuscaException("Para buscar jogos, cole sua chave do RAWG nas Configurações.");

    // A chave do TMDB pode ser a "Chave da API" (v3, curta) ou o "Token de leitura" (v4, longo)
    private string? Bearer() => ChaveTmdb() is { Length: > 40 } token ? token : null;

    private string UrlTmdb(string caminho, string parametros)
    {
        var chave = ChaveTmdb();
        var url = $"https://api.themoviedb.org/3/{caminho}?language=pt-BR&{parametros}";
        return chave.Length > 40 ? url : url + "&api_key=" + Uri.EscapeDataString(chave);
    }

    private async Task<JsonNode> Get(string url, string? bearer = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (bearer is not null) req.Headers.Authorization = new("Bearer", bearer);
        using var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return JsonNode.Parse(await resp.Content.ReadAsStringAsync())!;
    }

    private static string? Texto(JsonNode? n) =>
        n?.GetValueKind() == JsonValueKind.String && n.GetValue<string>() is { Length: > 0 } s ? s : null;

    private static int? Ano(string? data) => data is { Length: >= 4 } && int.TryParse(data[..4], out var a) ? a : null;

    private static string? Juntar(IEnumerable<string?>? itens) =>
        itens?.OfType<string>().Distinct().ToList() is { Count: > 0 } l ? string.Join(", ", l) : null;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
