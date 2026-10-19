using CdG.Data;
using CdG.Models;
using CdG.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CdG.Pages.Eventos
{
    [Authorize]
    public class AdiarModel : PageModel
    {
        private readonly NotificacaoService _notificacoes;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdiarModel(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    NotificacaoService notificacoes)
        {
            _context = context;
            _userManager = userManager;
            _notificacoes = notificacoes;
        }

        public int EventoId { get; private set; }

        [BindProperty]
        public string? VersaoOriginal { get; set; }

        public string NomeEvento { get; private set; } = string.Empty;

        [BindProperty]
        public DateTime? NovaData { get; set; }

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

            if (!PodeAdiar(evento))
                return BloquearAlteracao();

            EventoId = evento.Id;
            VersaoOriginal = VersaoEvento.Obter(evento);
            NomeEvento = evento.Nome;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            await using var transacao =
    await _context.Database.BeginTransactionAsync();

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

            if (!PodeAdiar(evento))
                return BloquearAlteracao();

            EventoId = evento.Id;
            NomeEvento = evento.Nome;

            if (VersaoOriginal != VersaoEvento.Obter(evento))
            {
                ModelState.AddModelError(string.Empty,
                    "O evento foi alterado desde que você abriu esta página. " +
                    "Recarregue a página e confira os dados antes de salvar novamente.");
                return Page();
            }

            if (!ModelState.IsValid)
                return Page();

            DateTime? novaDataUtc = null;

            if (NovaData.HasValue)
            {
                var fuso = TimeZoneInfo.FindSystemTimeZoneById(
                    "E. South America Standard Time");

                var horarioInformado = DateTime.SpecifyKind(
                    NovaData.Value,
                    DateTimeKind.Unspecified);

                if (fuso.IsInvalidTime(horarioInformado))
                {
                    ModelState.AddModelError(
                        nameof(NovaData),
                        "Informe um horário válido.");

                    return Page();
                }

                novaDataUtc = TimeZoneInfo.ConvertTimeToUtc(
                    horarioInformado, fuso);

                if (novaDataUtc.Value <= DateTime.UtcNow)
                {
                    ModelState.AddModelError(
                        nameof(NovaData),
                        "Informe uma data e um horário futuros.");

                    return Page();
                }
            }

            var agora = DateTime.UtcNow;
            var dataMudou = evento.DataHora != novaDataUtc;

            if (dataMudou)
            {
                var confirmados = await _context.VinculosEvento.Ativos()
                    .Where(vinculo =>
                        vinculo.EventoId == evento.Id &&
                        vinculo.StatusPresenca == StatusPresenca.Confirmada)
                    .ToListAsync();

                foreach (var vinculo in confirmados)
                {
                    vinculo.StatusPresenca = StatusPresenca.Pendente;
                    vinculo.PresencaAtualizadaEm = agora;
                }
            }

            evento.DataHora = novaDataUtc;

            evento.Status = novaDataUtc.HasValue
                ? StatusEvento.Agendado
                : StatusEvento.Adiado;

            evento.AtualizadoEm = agora;

            if (dataMudou)
            {
                string aviso;

                if (novaDataUtc.HasValue)
                {
                    var fusoAviso = TimeZoneInfo.FindSystemTimeZoneById(
                        "E. South America Standard Time");

                    var dataBrasilia = TimeZoneInfo
                        .ConvertTimeFromUtc(novaDataUtc.Value, fusoAviso)
                        .ToString("dd/MM/yyyy 'às' HH:mm");

                    aviso =
                        $"O evento \"{evento.Nome}\" foi reagendado para " +
                        $"{dataBrasilia} (horário de Brasília). " +
                        "Confira sua presença nos detalhes do evento.";
                }
                else
                {
                    aviso =
                        $"O evento \"{evento.Nome}\" foi adiado e está sem data definida. " +
                        "Você receberá outro aviso quando uma nova data for informada.";
                }

                await _notificacoes.AvisarParticipantesAsync(
                    evento.Id,
                    usuarioId,
                    TipoNotificacao.AlteracaoData,
                    aviso);
            }

            await _context.SaveChangesAsync();
            await transacao.CommitAsync();

            TempData["Mensagem"] = novaDataUtc.HasValue
                ? "Evento reagendado com sucesso."
                : "Evento adiado. A nova data será definida depois.";

            return RedirectToPage("/Eventos/Index");
        }

        private static bool PodeAdiar(Evento evento)
        {
            return evento.Status == StatusEvento.Agendado ||
                   evento.Status == StatusEvento.Adiado;
        }

        private IActionResult BloquearAlteracao()
        {
            TempData["Mensagem"] =
                "Eventos cancelados ou concluídos não podem ser reagendados.";

            return RedirectToPage("/Eventos/Index");
        }
    }
}
