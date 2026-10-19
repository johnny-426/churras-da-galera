using CdG.Data;
using CdG.Models;
using Microsoft.EntityFrameworkCore;

namespace CdG.Services
{
    public static class PermissoesEvento
    {
        public static Task<bool> EhOrganizadorAsync(
            ApplicationDbContext context,
            int eventoId,
            string usuarioId)
        {
            return context.VinculosEvento.AnyAsync(v =>
                v.EventoId == eventoId &&
                v.UsuarioId == usuarioId &&
                v.Papel == PapelEvento.Organizador &&
                v.SaiuEm == null &&
                v.OcultadoDoHistoricoEm == null);
        }
    }
}