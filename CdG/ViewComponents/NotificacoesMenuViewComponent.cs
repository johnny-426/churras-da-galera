using CdG.Data;
using CdG.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CdG.ViewComponents
{
    public class NotificacoesMenuViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificacoesMenuViewComponent(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (UserClaimsPrincipal.Identity?.IsAuthenticated != true)
            {
                return Content(string.Empty);
            }

            var usuarioId = _userManager.GetUserId(UserClaimsPrincipal);

            if (usuarioId == null)
            {
                return Content(string.Empty);
            }

            var totalNaoLidas = await _context.Notificacoes
                .CountAsync(n =>
                    n.DestinatarioId == usuarioId &&
                    n.LidaEm == null);

            return View(totalNaoLidas);
        }
    }
}