using CdG.Models;

namespace CdG.Services;

public static class ConsultasParticipacao
{
    // Mantém a expressão traduzível para SQL em todos os fluxos ativos.
    public static IQueryable<VinculoEvento> Ativos(this IQueryable<VinculoEvento> consulta)
        => consulta.Where(v => v.SaiuEm == null && v.OcultadoDoHistoricoEm == null);
}
