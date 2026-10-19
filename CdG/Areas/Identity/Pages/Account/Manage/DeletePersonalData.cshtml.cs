using System.ComponentModel.DataAnnotations;
using CdG.Data;
using CdG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CdG.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class DeletePersonalDataModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public DeletePersonalDataModel(ApplicationDbContext context,
        UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
    {
        _context = context;
        _users = users;
        _signIn = signIn;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();
    public bool RequirePassword { get; set; }
    public bool PossuiHistorico { get; set; }
    public class InputModel
    {
        [DataType(DataType.Password)]
        public string? Password { get; set; }
    }

    private async Task<bool> TemHistoricoAsync(string id) =>
        await _context.Eventos.AnyAsync(e => e.CriadorId == id) ||
        await _context.VinculosEvento.AnyAsync(v => v.UsuarioId == id) ||
        await _context.Convites.AnyAsync(c => c.DestinatarioId == id) ||
        await _context.Notificacoes.AnyAsync(n => n.DestinatarioId == id);

    public async Task<IActionResult> OnGetAsync()
    {
        var usuario = await _users.GetUserAsync(User);
        if (usuario == null) return Challenge();
        RequirePassword = await _users.HasPasswordAsync(usuario);
        PossuiHistorico = await TemHistoricoAsync(usuario.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var usuario = await _users.GetUserAsync(User);
        if (usuario == null) return Challenge();
        RequirePassword = await _users.HasPasswordAsync(usuario);
        PossuiHistorico = await TemHistoricoAsync(usuario.Id);
        if (PossuiHistorico)
        {
            ModelState.AddModelError(string.Empty,
                "A exclusão de contas com histórico ainda não está disponível. Nenhum dado foi apagado.");
            return Page();
        }
        if (RequirePassword && (string.IsNullOrEmpty(Input.Password) ||
            !await _users.CheckPasswordAsync(usuario, Input.Password)))
            ModelState.AddModelError("Input.Password", "Senha incorreta.");
        if (!ModelState.IsValid) return Page();

        try
        {
            var resultado = await _users.DeleteAsync(usuario);
            if (!resultado.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Não foi possível excluir a conta. Tente novamente.");
                return Page();
            }
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            // Um vínculo pode ter sido criado entre a consulta e a exclusão.
            PossuiHistorico = true;
            ModelState.AddModelError(string.Empty,
                "A conta possui registros relacionados e não pode ser excluída por esta tela.");
            return Page();
        }
        await _signIn.SignOutAsync();
        return Redirect("~/");
    }
}
