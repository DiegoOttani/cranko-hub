using CrankoHub.Components;
using CrankoHub.Models;
using Microsoft.AspNetCore.Components;

namespace CrankoHub.Services;

/// <summary>Algo que precisa da atenção do usuário (manutenção vencendo, cobrança chegando...).</summary>
/// <param name="Chave">Identifica o alerta (para não repetir avisos).</param>
/// <param name="Classe">Cor do badge: "critical", "serious", "warning" ou "".</param>
/// <param name="Link">Página que resolve o alerta.</param>
public record Alerta(string Chave, string Titulo, string Detalhe, string Badge, string Classe, string Link, MarkupString Icone);

/// <summary>Junta os alertas de todas as áreas do app, dos mais urgentes para os menos.</summary>
public static class Alertas
{
    /// <summary>Avisar das cobranças de assinatura com esta antecedência (dias).</summary>
    public const int DiasAssinatura = 3;

    /// <summary>Lembrar de pesar depois de tantos dias sem registro.</summary>
    public const int DiasSemPeso = 7;

    /// <summary>Lembrar de fazer backup depois de tantos dias (quando o automático está desligado).</summary>
    public const int DiasSemBackup = 30;

    private static readonly string[] Urgencia = ["critical", "serious", "warning", ""];

    public static List<Alerta> Gerar(EstadoApp d, DateOnly hoje)
    {
        var lista = new List<Alerta>();

        // Manutenção de todos os veículos
        foreach (var v in d.Carro.Veiculos)
        {
            var abast = CarroCalculos.DoVeiculo(d.Carro, v.Id);
            var manut = ManutencaoCalculos.DoVeiculo(d.Carro, v.Id);
            var odo = ManutencaoCalculos.OdometroAtual(abast, manut);
            var kmDia = ManutencaoCalculos.KmPorDia(abast, hoje);
            var nomeVeiculo = d.Carro.Veiculos.Count > 1 ? $" · {v.Nome}" : "";
            foreach (var s in ManutencaoCalculos.Lembretes(manut, odo, kmDia, hoje).Where(s => s.Vencida || s.EmBreve))
            {
                var (texto, classe) = s.Situacao;
                lista.Add(new($"manutencao:{v.Id}:{s.Tipo.Chave}", s.Tipo.Nome + nomeVeiculo, s.Restante, texto, classe,
                    $"carro?aba=manutencao&veiculo={v.Id}", Icones.Ferramenta));
            }
        }

        // Documentos vencidos ou vencendo (os do motorista aparecem uma vez só)
        foreach (var x in d.Carro.Documentos.Where(x => DocumentosCalculos.PrecisaAtencao(x, hoje)).OrderBy(x => x.Vencimento))
        {
            var veic = d.Carro.Veiculos.FirstOrDefault(v => v.Id == x.VeiculoId);
            if (x.VeiculoId is not null && veic is null) continue;
            var (texto, classe) = DocumentosCalculos.Situacao(x, hoje);
            var nome = TipoDocumento.NomeDe(x.Tipo) + (veic is not null && d.Carro.Veiculos.Count > 1 ? $" · {veic.Nome}" : "");
            var detalhe = string.Join(" · ", new[] { $"vence em {Formatacao.DataBR(x.Vencimento)}", x.Valor is not null ? Formatacao.Money(x.Valor) : null, x.Descricao }
                .Where(s => !string.IsNullOrEmpty(s)));
            lista.Add(new($"documento:{x.Id}", nome, detalhe, texto, classe == "" ? "warning" : classe,
                $"carro?aba=documentos{(veic is not null ? "&veiculo=" + veic.Id : "")}", Icones.Dados));
        }

        // Cobranças de assinaturas chegando
        foreach (var (a, data) in AssinaturasCalculos.CobrancasAte(d.Assinaturas.Where(a => a.Ativa), hoje, DiasAssinatura))
        {
            var (texto, classe) = AssinaturasCalculos.Prazo(data.DayNumber - hoje.DayNumber);
            lista.Add(new($"assinatura:{a.Id}:{Formatacao.Iso(data)}", $"{a.Nome} cobra {texto}",
                $"{Formatacao.Money(a.Valor)}{(string.IsNullOrEmpty(a.Pagamento) ? "" : " · " + a.Pagamento)}",
                Formatacao.DataBR(data), classe == "" ? "warning" : classe, "assinaturas", Icones.Assinaturas));
        }

        // Peso sem registro há muito tempo (só para quem já usa a Dieta)
        var ultimoPeso = d.Dieta.Registros.Count > 0 ? d.Dieta.Registros.Max(r => r.Data) : (DateOnly?)null;
        if (ultimoPeso is DateOnly up && hoje.DayNumber - up.DayNumber >= DiasSemPeso)
        {
            var dias = hoje.DayNumber - up.DayNumber;
            lista.Add(new("peso", "Hora de se pesar", $"Último registro há {dias} dias ({Formatacao.DataBR(up)})",
                $"{dias} dias", "", "dieta", Icones.Balanca));
        }

        // Backup manual esquecido
        var cfg = d.Config;
        var temDados = d.Dieta.Registros.Count + d.Carro.Abastecimentos.Count + d.Carro.Manutencoes.Count + d.Assinaturas.Count > 0;
        if (temDados && !cfg.BackupAutomatico && (cfg.UltimoBackup is null || hoje.DayNumber - cfg.UltimoBackup.Value.DayNumber >= DiasSemBackup))
        {
            lista.Add(new("backup", "Faça um backup dos seus dados",
                cfg.UltimoBackup is DateOnly ub ? $"Último backup em {Formatacao.DataBR(ub)}" : "Ative o backup automático para não perder nada",
                "backup", "", "configuracoes", Icones.Dados));
        }

        return lista.OrderBy(a => Array.IndexOf(Urgencia, a.Classe)).ToList();
    }
}
