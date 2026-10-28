namespace CrankoHub.Models;

/// <summary>Algo assistido, jogado ou lido — um item da estante de Entretenimento.</summary>
public class ItemEntretenimento
{
    public string Id { get; set; } = "";

    /// <summary>Chave da categoria ("filme", "jogo"...). Ver <see cref="CategoriaEntretenimento"/>.</summary>
    public string Categoria { get; set; } = "filme";

    public string Titulo { get; set; } = "";

    public int? Ano { get; set; }

    /// <summary>Quando assistiu/terminou.</summary>
    public DateOnly? Data { get; set; }

    /// <summary>De 0,5 a 5, em meias estrelas.</summary>
    public double? Nota { get; set; }

    /// <summary>"concluido", "andamento" ou "quero". Ver <see cref="StatusEntretenimento"/>.</summary>
    public string Status { get; set; } = "concluido";

    /// <summary>Nome do arquivo da capa na pasta de capas (ver <see cref="Services.CapasService"/>).</summary>
    public string? Capa { get; set; }

    public string? Sinopse { get; set; }

    /// <summary>Diretor, autor, estúdio...</summary>
    public string? Criadores { get; set; }

    public string? Generos { get; set; }

    /// <summary>De onde vieram os dados ("tmdb", "rawg", "anilist", "openlibrary").</summary>
    public string? Fonte { get; set; }

    public string? FonteId { get; set; }

    public string? Obs { get; set; }
}

/// <summary>Uma prateleira da estante e a API usada para buscar os dados dela.</summary>
public record CategoriaEntretenimento(string Chave, string Nome, string Singular, string Verbo, string Api)
{
    public static readonly IReadOnlyList<CategoriaEntretenimento> Todas =
    [
        new("filme", "Filmes", "filme", "Assistido", "tmdb-filme"),
        new("serie", "Séries", "série", "Assistida", "tmdb-serie"),
        new("animacao", "Animações", "animação", "Assistida", "tmdb-filme"),
        new("anime", "Animes", "anime", "Assistido", "anilist"),
        new("jogo", "Jogos", "jogo", "Zerado", "rawg"),
        new("livro", "Livros", "livro", "Lido", "openlibrary"),
    ];

    public static CategoriaEntretenimento Buscar(string chave) => Todas.FirstOrDefault(c => c.Chave == chave) ?? Todas[0];
}

public record StatusEntretenimento(string Chave, string Nome)
{
    public static readonly IReadOnlyList<StatusEntretenimento> Todos =
    [
        new("concluido", "Concluído"),
        new("andamento", "Em andamento"),
        new("quero", "Quero ver"),
    ];

    public static StatusEntretenimento Buscar(string chave) => Todos.FirstOrDefault(s => s.Chave == chave) ?? Todos[0];
}
