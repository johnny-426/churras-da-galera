using System.ComponentModel.DataAnnotations;
using CdG.Services;
using CdG.Data;
using CdG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CdG.Pages.Eventos
{
    [Authorize]
    public class ConvidarModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        private readonly NotificacaoService _notificacoes;

        public ConvidarModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            NotificacaoService notificacoes)
        {
            _context = context;
            _userManager = userManager;
            _notificacoes = notificacoes;
        }
        public int EventoId { get; private set; }

        public string NomeEvento { get; private set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Informe o e-mail da pessoa.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [TempData]
        public string? Mensagem { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            if (!await PermissoesEvento.EhOrganizadorAsync(
        _context, id, usuarioId))
            {
                return NotFound();
            }

            var evento = await _context.Eventos
                .AsNoTracking()
                .FirstOrDefaultAsync(evento => evento.Id == id);

            if (evento == null)
                return NotFound();

            if (!PodeConvidar(evento))
                return BloquearEnvio();

            EventoId = evento.Id;
            NomeEvento = evento.Nome;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            // Mantém a verificação do evento e as inserções
            // dentro da mesma transação.
            await using var transacao =
                await _context.Database.BeginTransactionAsync();

            // Bloqueia esta linha durante a operação, para que
            // cancelar ou reagendar não ocorra no meio do envio.
            var eventos = await _context.Eventos
    .FromSqlInterpolated($"""
        SELECT *
        FROM "Eventos"
        WHERE "Id" = {id}
        FOR UPDATE
        """)
    .ToListAsync();

            var evento = eventos.SingleOrDefault();

            if (evento == null)
                return NotFound();

            if (!await PermissoesEvento.EhOrganizadorAsync(
        _context, evento.Id, usuarioId))
            {
                return NotFound();
            }

            if (!PodeConvidar(evento))
                return BloquearEnvio();

            EventoId = evento.Id;
            NomeEvento = evento.Nome;

            if (!ModelState.IsValid)
                return Page();

            var destinatario = await _userManager
                .FindByEmailAsync(Email.Trim());

            if (destinatario == null)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "Não encontramos uma conta com esse e-mail.");

                return Page();
            }

            if (destinatario.Id == usuarioId)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "Você já é o organizador deste evento.");

                return Page();
            }

            var saiuAntes = await _context.VinculosEvento.AnyAsync(v =>
                v.EventoId == id && v.UsuarioId == destinatario.Id && v.SaiuEm != null);
            if (saiuAntes)
            {
                ModelState.AddModelError(nameof(Email),
                    "Essa pessoa saiu do evento. O retorno ao mesmo evento ainda não está disponível.");
                return Page();
            }

            var possuiConvite = await _context.Convites
                .AnyAsync(convite =>
                    convite.EventoId == id &&
                    convite.DestinatarioId == destinatario.Id);

            var possuiVinculo = await _context.VinculosEvento
                .AnyAsync(vinculo =>
                    vinculo.EventoId == id &&
                    vinculo.UsuarioId == destinatario.Id);

            if (possuiConvite || possuiVinculo)
            {
                ModelState.AddModelError(
                    nameof(Email),
                    "Essa pessoa já foi convidada ou já está vinculada ao evento.");

                return Page();
            }

            var agora = DateTime.UtcNow;

            var convite = new Convite
            {
                EventoId = evento.Id,
                DestinatarioId = destinatario.Id,
                Status = StatusConvite.Enviado,
                EnviadoEm = agora
            };

            var vinculo = new VinculoEvento
            {
                EventoId = evento.Id,
                UsuarioId = destinatario.Id,
                Papel = PapelEvento.Participante,
                StatusPresenca = StatusPresenca.Pendente,
                PresencaAtualizadaEm = agora
            };

            _context.Convites.Add(convite);
            _context.VinculosEvento.Add(vinculo);

            _notificacoes.Criar(
    destinatario.Id,
    evento.Id,
    TipoNotificacao.ConviteRecebido,
    $"Você recebeu um convite para o evento \"{evento.Nome}\".");

            try
            {
                await _context.SaveChangesAsync();
                await transacao.CommitAsync();
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                })
            {
                await transacao.RollbackAsync();

                ModelState.AddModelError(
                    nameof(Email),
                    "Essa pessoa já foi convidada. Atualize a página.");

                return Page();
            }

            Mensagem = "Convite enviado com sucesso.";

            return RedirectToPage(new { id = evento.Id });
        }

        private static bool PodeConvidar(Evento evento)
        {
            return evento.Status == StatusEvento.Agendado &&
                   evento.DataHora.HasValue &&
                   evento.DataHora.Value > DateTime.UtcNow;
        }

        private IActionResult BloquearEnvio()
        {
            TempData["Mensagem"] =
                "Convites só podem ser enviados para eventos agendados com data futura.";

            return RedirectToPage("/Eventos/Index");
        }
    }
}
