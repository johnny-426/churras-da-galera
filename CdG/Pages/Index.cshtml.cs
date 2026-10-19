using CdG.Services;
using CdG.Data;
using CdG.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CdG.Pages
{
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

        public bool EstaConectado { get; set; }

        public string NomeExibido { get; set; } = string.Empty;

        public int TotalProximosEventos { get; set; }

        public int ConvitesParaResponder { get; set; }

        public int NotificacoesNaoLidas { get; set; }

        public List<Evento> ProximosEventos { get; set; } = new();

        public async Task OnGetAsync()
        {
            var usuario = await _userManager.GetUserAsync(User);

            if (usuario == null)
            {
                return;
            }

            EstaConectado = true;
            NomeExibido = usuario.Nome ?? string.Empty;

            var agora = DateTime.UtcNow;

            var eventosConfirmados = _context.Eventos
                .AsNoTracking()
                .Where(e =>
                    e.Status == StatusEvento.Agendado &&
                    e.DataHora.HasValue &&
                    e.DataHora.Value > agora &&
                    _context.VinculosEvento.Any(v => v.SaiuEm == null && v.OcultadoDoHistoricoEm == null &&
                        v.EventoId == e.Id &&
                        v.UsuarioId == usuario.Id &&
                        v.StatusPresenca == StatusPresenca.Confirmada));

            TotalProximosEventos = await eventosConfirmados.CountAsync();

            ProximosEventos = await eventosConfirmados
                .OrderBy(e => e.DataHora)
                .ThenBy(e => e.Id)
                .Take(5)
                .ToListAsync();

            ConvitesParaResponder = await _context.Convites
                .CountAsync(c =>
                    c.DestinatarioId == usuario.Id &&
                    c.Status == StatusConvite.Enviado &&
                    _context.VinculosEvento.Any(v => v.SaiuEm == null && v.OcultadoDoHistoricoEm == null &&
                        v.EventoId == c.EventoId && v.UsuarioId == usuario.Id) &&
                    c.Evento.Status == StatusEvento.Agendado &&
                    c.Evento.DataHora.HasValue &&
                    c.Evento.DataHora.Value > agora);

            NotificacoesNaoLidas = await _context.Notificacoes
                .CountAsync(n =>
                    n.DestinatarioId == usuario.Id &&
                    n.LidaEm == null);
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
