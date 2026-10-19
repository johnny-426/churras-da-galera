using System.ComponentModel.DataAnnotations;
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
    public class EditarModel : PageModel
    {
        private readonly NotificacaoService _notificacoes;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EditarModel(
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

        [BindProperty]
        public DadosEdicao Entrada { get; set; } = new();

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

            if (!PodeEditar(evento))
            {
                TempData["Mensagem"] =
                    "Eventos cancelados ou concluídos não podem ser editados.";

                return RedirectToPage("/Eventos/Index");
            }

            EventoId = evento.Id;
            VersaoOriginal = VersaoEvento.Obter(evento);

            Entrada = new DadosEdicao
            {
                Nome = evento.Nome,
                Descricao = evento.Descricao,
                Local = evento.Local,
                LimiteParticipantes = evento.LimiteParticipantes
            };

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

            if (!PodeEditar(evento))
            {
                TempData["Mensagem"] =
                    "Eventos cancelados ou concluídos não podem ser editados.";

                return RedirectToPage("/Eventos/Index");
            }

            EventoId = evento.Id;

            if (VersaoOriginal != VersaoEvento.Obter(evento))
            {
                ModelState.AddModelError(string.Empty,
                    "O evento foi alterado desde que você abriu esta página. " +
                    "Recarregue a página e confira os dados antes de salvar novamente.");
                return Page();
            }

            if (!ModelState.IsValid)
                return Page();

            if (Entrada.LimiteParticipantes.HasValue)
            {
                var totalConfirmados = await _context.VinculosEvento.Ativos()
                    .CountAsync(vinculo =>
                        vinculo.EventoId == evento.Id &&
                        vinculo.StatusPresenca == StatusPresenca.Confirmada);

                if (Entrada.LimiteParticipantes.Value < totalConfirmados)
                {
                    ModelState.AddModelError(
                        "Entrada.LimiteParticipantes",
                        $"O evento já tem {totalConfirmados} presença(s) confirmada(s).");

                    return Page();
                }
            }

            var novoLocal = Entrada.Local.Trim();

            var localMudou = !string.Equals(
                evento.Local,
                novoLocal,
                StringComparison.Ordinal);

            evento.Nome = Entrada.Nome.Trim();
            evento.Descricao = Entrada.Descricao?.Trim();
            evento.Local = novoLocal;
            evento.LimiteParticipantes = Entrada.LimiteParticipantes;
            evento.AtualizadoEm = DateTime.UtcNow;

            if (localMudou)
            {
                await _notificacoes.AvisarParticipantesAsync(
                    evento.Id,
                    usuarioId,
                    TipoNotificacao.AlteracaoLocal,
                    $"O local do evento \"{evento.Nome}\" foi alterado para: " +
                    $"{evento.Local}. Confira os detalhes.");
            }

            await _context.SaveChangesAsync();
            await transacao.CommitAsync();

            TempData["Mensagem"] = "Evento atualizado com sucesso.";

            return RedirectToPage("/Eventos/Index");
        }

        private static bool PodeEditar(Evento evento)
        {
            return evento.Status == StatusEvento.Agendado ||
                   evento.Status == StatusEvento.Adiado;
        }

        public class DadosEdicao
        {
            [Required(ErrorMessage = "Informe o nome do evento.")]
            [StringLength(100)]
            public string Nome { get; set; } = string.Empty;

            [StringLength(2000)]
            public string? Descricao { get; set; }

            [Required(ErrorMessage = "Informe o local.")]
            [StringLength(300)]
            public string Local { get; set; } = string.Empty;

            [Range(1, int.MaxValue,
                ErrorMessage = "O limite deve ser maior que zero.")]
            public int? LimiteParticipantes { get; set; }
        }
    }
}
