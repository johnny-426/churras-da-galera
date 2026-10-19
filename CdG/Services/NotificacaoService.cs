using CdG.Data;
using CdG.Models;
using Microsoft.EntityFrameworkCore;

namespace CdG.Services
{
    public class NotificacaoService
    {
        private readonly ApplicationDbContext _context;

        public NotificacaoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public void Criar(
            string destinatarioId,
            int eventoId,
            TipoNotificacao tipo,
            string mensagem)
        {
            var notificacao = new Notificacao
            {
                DestinatarioId = destinatarioId,
                EventoId = eventoId,
                Tipo = tipo,
                Mensagem = mensagem,
                CriadaEm = DateTime.UtcNow
            };

            _context.Notificacoes.Add(notificacao);
        }
        public async Task AvisarParticipantesAsync(
    int eventoId,
    string autorId,
    TipoNotificacao tipo,
    string mensagem)
        {
            var destinatarios = await _context.VinculosEvento.Ativos()
                .Where(vinculo =>
                    vinculo.EventoId == eventoId &&
                    vinculo.UsuarioId != autorId)
                .Select(vinculo => vinculo.UsuarioId)
                .Distinct()
                .ToListAsync();

            foreach (var destinatarioId in destinatarios)
            {
                Criar(destinatarioId, eventoId, tipo, mensagem);
            }
        }
    }
}
