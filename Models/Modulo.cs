using CrankoHub.Components;
using Microsoft.AspNetCore.Components;

namespace CrankoHub.Models;

/// <summary>Uma área do app que pode ser escondida em Configurações (menu, Início, alertas e avisos).</summary>
public record Modulo(string Id, string Nome, string Descricao, MarkupString Icone)
{
    public const string Dieta = "dieta";
    public const string Carro = "carro";
    public const string Assinaturas = "assinaturas";
    public const string Entretenimento = "entretenimento";
    public const string Tarefas = "tarefas";

    public static readonly IReadOnlyList<Modulo> Todos =
    [
        new(Dieta, "Dieta", "Peso, medidas, IMC e meta", Icones.Dieta),
        new(Carro, "Carro", "Abastecimentos, manutenção e documentos", Icones.Carro),
        new(Assinaturas, "Assinaturas", "Serviços recorrentes e cobranças", Icones.Assinaturas),
        new(Entretenimento, "Entretenimento", "Livros, filmes, séries, animes e jogos", Icones.Entretenimento),
        new(Tarefas, "Tarefas", "Quadros Kanban e tarefas do dia", Icones.Tarefas),
    ];

    /// <summary>O link do módulo é o próprio Id (ex.: "dieta").</summary>
    public string Link => Id;

    public static Modulo? Buscar(string id) => Todos.FirstOrDefault(m => m.Id == id);

    public static bool Visivel(Configuracoes cfg, string id) => !cfg.ModulosOcultos.Contains(id);

    public static IEnumerable<Modulo> Visiveis(Configuracoes cfg) => Todos.Where(m => Visivel(cfg, m.Id));
}
