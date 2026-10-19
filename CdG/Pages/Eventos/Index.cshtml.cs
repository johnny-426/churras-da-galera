using CdG.Data;
using CdG.Services;
using CdG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CdG.Pages.Eventos
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ParticipacaoService _participacoes;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager, ParticipacaoService participacoes)
        {
            _context = context;
            _participacoes = participacoes;
            _userManager = userManager;
        }

        public List<VinculoEvento> Participacoes { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Busca { get; set; }

        [BindProperty(SupportsGet = true)]
        public StatusEvento? Situacao { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool Ocultos { get; set; }

        [TempData]
        public string? Mensagem { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid ||
                (Situacao.HasValue &&
                 !Enum.IsDefined(typeof(StatusEvento), Situacao.Value)))
            {
                return BadRequest("Filtro de situação inválido.");
            }

            var consulta = _context.VinculosEvento
                .AsNoTracking()
                .Include(v => v.Evento)
                .Where(v => v.UsuarioId == usuarioId &&
                    (Ocultos ? v.OcultadoDoHistoricoEm != null : v.OcultadoDoHistoricoEm == null));

            Busca = Busca?.Trim();

            if (!string.IsNullOrWhiteSpace(Busca))
            {
                var termo = Busca.ToLowerInvariant();

                consulta = consulta.Where(v =>
                    v.Evento.Nome.ToLower().Contains(termo));
            }

            if (Situacao.HasValue)
            {
                var situacao = Situacao.Value;

                consulta = consulta.Where(v =>
                    v.Evento.Status == situacao);
            }

            Participacoes = await consulta
                .OrderByDescending(v => v.Evento.CriadoEm)
                .ThenByDescending(v => v.EventoId)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostOcultarAsync(int id)
        {
            var usuarioId = _userManager.GetUserId(User);
            if (usuarioId == null) return Challenge();
            Mensagem = await _participacoes.DefinirOcultacaoAsync(id, usuarioId, true);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRestaurarAsync(int id)
        {
            var usuarioId = _userManager.GetUserId(User);
            if (usuarioId == null) return Challenge();
            Mensagem = await _participacoes.DefinirOcultacaoAsync(id, usuarioId, false);
            return RedirectToPage(new { Ocultos = true });
        }

        public string FormatarData(DateTime? data)
        {
            if (!data.HasValue)
            {
                return "Data a definir";
            }

            var fuso = TimeZoneInfo.FindSystemTimeZoneById(
                "E. South America Standard Time");

            return TimeZoneInfo
                .ConvertTimeFromUtc(data.Value, fuso)
                .ToString("dd/MM/yyyy HH:mm");
        }
    }
}
