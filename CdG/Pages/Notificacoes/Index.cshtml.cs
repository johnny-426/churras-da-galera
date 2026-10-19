using CdG.Data;
using CdG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CdG.Pages.Notificacoes
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private const int TamanhoPagina = 10;

        [BindProperty(SupportsGet = true)]
        public int Pagina { get; set; } = 1;

        public int TotalResultados { get; set; }

        public int TotalPaginas { get; set; }

        private readonly ApplicationDbContext _context;

        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;

        }

        public HashSet<int> EventosAcessiveis { get; private set; } = new();

        public List<Notificacao> Notificacoes { get; set; } = new();

        public int TotalNaoLidas { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool SomenteNaoLidas { get; set; }

        [TempData]
        public string? MensagemStatus { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
            {
                return Challenge();
            }

            var consulta = _context.Notificacoes
                .AsNoTracking()
                .Where(n => n.DestinatarioId == usuarioId);

            TotalNaoLidas = await consulta
                .CountAsync(n => n.LidaEm == null);

            if (SomenteNaoLidas)
            {
                consulta = consulta.Where(n => n.LidaEm == null);
            }

            TotalResultados = await consulta.CountAsync();

            TotalPaginas = Math.Max(
                1,
                (int)Math.Ceiling(TotalResultados / (double)TamanhoPagina));

            Pagina = Math.Clamp(Pagina, 1, TotalPaginas);

            Notificacoes = await consulta
                .OrderByDescending(n => n.CriadaEm)
                .ThenByDescending(n => n.Id)
                .Skip((Pagina - 1) * TamanhoPagina)
                .Take(TamanhoPagina)
                .ToListAsync();

            var ids = Notificacoes.Select(n => n.EventoId).ToList();
            EventosAcessiveis = (await _context.VinculosEvento
                .Where(v => v.UsuarioId == usuarioId && ids.Contains(v.EventoId) &&
                    v.SaiuEm == null && v.OcultadoDoHistoricoEm == null)
                .Select(v => v.EventoId).ToListAsync()).ToHashSet();
            return Page();
        }

        public async Task<IActionResult> OnPostMarcarLidaAsync(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
            {
                return Challenge();
            }

            var notificacao = await _context.Notificacoes
                .SingleOrDefaultAsync(n =>
                    n.Id == id &&
                    n.DestinatarioId == usuarioId);

            if (notificacao == null)
            {
                return NotFound();
            }

            if (notificacao.LidaEm == null)
            {
                notificacao.LidaEm = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage(new { SomenteNaoLidas, Pagina });
        }

        public async Task<IActionResult> OnPostMarcarTodasAsync()
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
            {
                return Challenge();
            }

            var naoLidas = await _context.Notificacoes
                .Where(n =>
                    n.DestinatarioId == usuarioId &&
                    n.LidaEm == null)
                .ToListAsync();

            var agora = DateTime.UtcNow;

            foreach (var notificacao in naoLidas)
            {
                notificacao.LidaEm = agora;
            }

            await _context.SaveChangesAsync();

            MensagemStatus = naoLidas.Count == 0
                ? "Não havia notificações pendentes de leitura."
                : $"{naoLidas.Count} notificação(ões) marcada(s) como lida(s).";

            return RedirectToPage(new { SomenteNaoLidas });
        }

        public string FormatarData(DateTime data)
        {
            var fuso = TimeZoneInfo.FindSystemTimeZoneById(
                "E. South America Standard Time");

            return TimeZoneInfo
                .ConvertTimeFromUtc(data, fuso)
                .ToString("dd/MM/yyyy HH:mm");
        }
    }
}
