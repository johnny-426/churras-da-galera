using CdG.Services;
using CdG.Data;
using CdG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CdG.Pages.Convites
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<Convite> Convites { get; private set; } = new();

        [TempData]
        public string? Mensagem { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            Convites = await _context.Convites
                .AsNoTracking()
                .Include(convite => convite.Evento)
                .Where(convite => convite.DestinatarioId == usuarioId &&
                    _context.VinculosEvento.Any(v => v.SaiuEm == null && v.OcultadoDoHistoricoEm == null &&
                        v.EventoId == convite.EventoId && v.UsuarioId == usuarioId))
                .OrderByDescending(convite => convite.EnviadoEm)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostResponderAsync(
    int id, string resposta, long? dataTicks)
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            if (resposta != "aceitar" && resposta != "recusar")
                return BadRequest();

            // Descobre o evento somente se o convite pertence ao usuário.
            var eventoId = await _context.Convites
                .AsNoTracking()
                .Where(convite =>
                    convite.Id == id &&
                    convite.DestinatarioId == usuarioId)
                .Select(convite => (int?)convite.EventoId)
                .SingleOrDefaultAsync();

            if (eventoId == null)
                return NotFound();

            await using var transacao =
                await _context.Database.BeginTransactionAsync();

            // Uma confirmação por vez neste evento.
            var eventos = await _context.Eventos
                .FromSqlInterpolated($"""
            SELECT *
            FROM "Eventos"
            WHERE "Id" = {eventoId.Value}
            FOR UPDATE
            """)
                .ToListAsync();

            var evento = eventos.SingleOrDefault();

            if (evento == null)
                return NotFound();

            // Consulta novamente após obter o bloqueio.
            var convite = await _context.Convites
                .SingleOrDefaultAsync(convite =>
                    convite.Id == id &&
                    convite.DestinatarioId == usuarioId);

            if (convite == null)
                return NotFound();

            if (convite.Status != StatusConvite.Enviado)
            {
                Mensagem = "Esse convite já foi respondido.";
                return RedirectToPage();
            }

            if (evento.Status != StatusEvento.Agendado ||
                !evento.DataHora.HasValue ||
                evento.DataHora.Value <= DateTime.UtcNow)
            {
                Mensagem =
                    "Não é possível responder: o evento precisa estar agendado com data futura.";

                return RedirectToPage();
            }

            // Evita confirmar uma data diferente da exibida na tela.
            if (dataTicks != evento.DataHora.Value.Ticks)
            {
                Mensagem =
                    "A data do evento mudou. Confira os detalhes antes de responder.";

                return RedirectToPage();
            }

            var vinculo = await _context.VinculosEvento.Ativos()
                .SingleOrDefaultAsync(vinculo =>
                    vinculo.EventoId == evento.Id &&
                    vinculo.UsuarioId == usuarioId);

            if (vinculo == null)
            {
                Mensagem =
                    "O vínculo de participação não foi encontrado. Avise o organizador.";

                return RedirectToPage();
            }

            var aceitar = resposta == "aceitar";

            if (aceitar &&
                vinculo.StatusPresenca != StatusPresenca.Confirmada &&
                evento.LimiteParticipantes.HasValue)
            {
                var confirmados = await _context.VinculosEvento.Ativos()
                    .CountAsync(vinculo =>
                        vinculo.EventoId == evento.Id &&
                        vinculo.StatusPresenca == StatusPresenca.Confirmada);

                if (confirmados >= evento.LimiteParticipantes.Value)
                {
                    Mensagem =
                        "O evento está lotado. Seu convite continua aguardando resposta.";

                    return RedirectToPage();
                }
            }

            var agora = DateTime.UtcNow;

            convite.Status = aceitar
                ? StatusConvite.Aceito
                : StatusConvite.Recusado;

            convite.RespondidoEm = agora;

            vinculo.StatusPresenca = aceitar
                ? StatusPresenca.Confirmada
                : StatusPresenca.Recusada;

            vinculo.PresencaAtualizadaEm = agora;

            if (aceitar)
            {
                vinculo.ParticipacaoAceitaEm ??= agora;
            }

            await _context.SaveChangesAsync();
            await transacao.CommitAsync();

            Mensagem = aceitar
                ? "Convite aceito! Sua presença está confirmada."
                : "Convite recusado.";

            return RedirectToPage();
        }

    }
}
