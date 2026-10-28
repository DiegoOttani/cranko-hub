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
/// Busca dados e capas nas APIs públicas. Sem nenhuma chave: Wikipedia em português + Wikidata
/// (filmes, séries, animações e jogos; pôster das séries pelo TVmaze), AniList (animes) e Open Library (livros).
/// Com as chaves opcionais das Configurações, filmes/séries usam o TMDB e jogos o RAWG, que trazem mais resultados.
/// </summary>
public partial class EntretenimentoBusca(HttpClient http, ArmazenamentoService store)
{
    private const int Maximo = 12;

    /// <summary>Identificação nas requisições (a Wikimedia limita quem não diz quem é).</summary>
    public const string UserAgent = "CrankoHub/1.0 (https://github.com/DiegoOttani/cranko-hub)";

    public async Task<List<ResultadoBusca>> Buscar(CategoriaEntretenimento cat, string termo)
    {
        termo = termo.Trim();
        if (termo.Length == 0) return [];
        try
        {
            return cat.Api switch
            {
                "tmdb-filme" when ChaveTmdb() is null => await Wikipedia("Info/Filme", termo),
                "tmdb-serie" when ChaveTmdb() is null => await Wikipedia("Info/Televisão", termo),
                "rawg" when ChaveRawg() is null => await Wikipedia("Info/Jogo eletrônico", termo),
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
                case "wikipedia":
                    return await DetalharWikipedia(r);
                case "rawg":
                {
                    var j = await Get($"https://api.rawg.io/api/games/{r.FonteId}?key={Uri.EscapeDataString(ChaveRawg() ?? "")}");
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
        var j = await Get($"https://api.rawg.io/api/games?page_size={Maximo}&search={Uri.EscapeDataString(termo)}&key={Uri.EscapeDataString(ChaveRawg() ?? "")}");
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

    // Busca só entre as páginas com a ficha (infobox) do tipo certo: Info/Filme, Info/Televisão, Info/Jogo eletrônico
    private async Task<List<ResultadoBusca>> Wikipedia(string ficha, string termo)
    {
        var j = await Get(UrlWikipedia("generator=search&gsrlimit=" + Maximo
            + "&gsrsearch=" + Uri.EscapeDataString($"{termo} hastemplate:\"{ficha}\"")
            + "&prop=pageimages&piprop=thumbnail&pithumbsize=600"));
        return (j["query"]?["pages"]?.AsArray() ?? [])
            .OrderBy(x => (int?)x?["index"] ?? 99)
            .Select(x =>
            {
                // "Dune (2021)", "Inside Out (filme de 2015)": tira o complemento do título e aproveita o ano
                var titulo = Texto(x?["title"]) ?? "";
                var m = Complemento().Match(titulo);
                int? ano = m.Success && AnoNoTexto().Match(m.Value) is { Success: true } a ? int.Parse(a.Value) : null;
                return new ResultadoBusca(m.Success ? titulo[..m.Index] : titulo, ano,
                    Texto(x?["thumbnail"]?["source"]), null, null, null, "wikipedia", x?["pageid"]?.ToString() ?? "");
            }).ToList();
    }

    // Resumo da Wikipedia + direção/criação, gêneros e data da Wikidata (e pôster do TVmaze para séries)
    private async Task<ResultadoBusca> DetalharWikipedia(ResultadoBusca r)
    {
        var pagina = (await Get(UrlWikipedia($"pageids={r.FonteId}&prop=extracts|pageprops&exintro=1&explaintext=1&exsentences=4&ppprop=wikibase_item")))
            ["query"]?["pages"]?[0];
        var resumo = Texto(pagina?["extract"]);
        // Muitos artigos usam o título original e trazem o brasileiro no resumo: "Inside Out (bra: Divertida Mente; prt: ...)"
        var tituloBr = resumo is not null && TituloBrasil().Match(resumo) is { Success: true } t ? t.Groups[1].Value.Trim() : null;
        r = r with { Sinopse = resumo ?? r.Sinopse, Titulo = tituloBr ?? r.Titulo };
        if (Texto(pagina?["pageprops"]?["wikibase_item"]) is not string qid) return r;

        var claims = (await Get($"https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&props=claims&ids={qid}"))
            ["entities"]?[qid]?["claims"];
        List<string> Itens(params string[] props) => props
            .SelectMany(p => claims?[p]?.AsArray() ?? [])
            .Select(c => Texto(c?["mainsnak"]?["datavalue"]?["value"]?["id"]))
            .OfType<string>().Distinct().ToList();
        // Direção (filmes), criação (séries) ou desenvolvimento (jogos)
        var criadores = Itens("P57", "P170", "P178").Take(3).ToList();
        var generos = Itens("P136").Take(3).ToList();
        var nomes = await NomesWikidata(criadores.Concat(generos));
        string? Nomes(List<string> ids) => Juntar(ids.Select(id => nomes.GetValueOrDefault(id)));

        // Primeira data de publicação/estreia
        var ano = new[] { "P577", "P580" }
            .SelectMany(p => claims?[p]?.AsArray() ?? [])
            .Select(c => Texto(c?["mainsnak"]?["datavalue"]?["value"]?["time"]) is string t ? Ano(t.TrimStart('+')) : null)
            .Where(a => a is not null).Min();

        var capa = r.UrlCapa;
        if (claims?["P8600"]?[0]?["mainsnak"]?["datavalue"]?["value"] is JsonNode tvmaze && Texto(tvmaze) is string idTvmaze)
            try { capa = Texto((await Get($"https://api.tvmaze.com/shows/{idTvmaze}"))["image"]?["original"]) ?? capa; } catch { }

        return r with { Ano = ano ?? r.Ano, Criadores = Nomes(criadores) ?? r.Criadores, Generos = Nomes(generos) ?? r.Generos, UrlCapa = capa };
    }

    /// <summary>Nomes em português (ou inglês) dos itens da Wikidata.</summary>
    private async Task<Dictionary<string, string>> NomesWikidata(IEnumerable<string> ids)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return [];
        var j = await Get($"https://www.wikidata.org/w/api.php?action=wbgetentities&format=json&props=labels&languages=pt-br|pt|en&ids={string.Join('|', lista)}");
        var nomes = new Dictionary<string, string>();
        foreach (var id in lista)
        {
            var rotulos = j["entities"]?[id]?["labels"];
            if ((Texto(rotulos?["pt-br"]?["value"]) ?? Texto(rotulos?["pt"]?["value"]) ?? Texto(rotulos?["en"]?["value"])) is string nome)
                nomes[id] = nome;
        }
        return nomes;
    }

    private static string UrlWikipedia(string parametros) =>
        "https://pt.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&" + parametros;

    // ---- Auxiliares ----

    /// <summary>Chave do TMDB, se o usuário colou uma (é opcional).</summary>
    private string? ChaveTmdb() => store.Dados.Config.ChaveTmdb?.Trim() is { Length: > 0 } k ? k : null;

    /// <summary>Chave do RAWG, se o usuário colou uma (é opcional).</summary>
    private string? ChaveRawg() => store.Dados.Config.ChaveRawg?.Trim() is { Length: > 0 } k ? k : null;

    // A chave do TMDB pode ser a "Chave da API" (v3, curta) ou o "Token de leitura" (v4, longo)
    private string? Bearer() => ChaveTmdb() is { Length: > 40 } token ? token : null;

    private string UrlTmdb(string caminho, string parametros)
    {
        var chave = ChaveTmdb() ?? "";
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

    [GeneratedRegex(@"\s*\([^)]*\)$")]
    private static partial Regex Complemento();

    [GeneratedRegex(@"\b(1[89]|20)\d{2}\b")]
    private static partial Regex AnoNoTexto();

    [GeneratedRegex(@"\bbra:\s*([^;)]+)")]
    private static partial Regex TituloBrasil();
}
