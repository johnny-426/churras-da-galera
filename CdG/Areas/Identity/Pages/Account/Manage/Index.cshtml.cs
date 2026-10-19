using System.ComponentModel.DataAnnotations;
using CdG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CdG.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Email { get; set; } = string.Empty;

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Informe seu nome.")]
            [StringLength(100,
                ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
            [Display(Name = "Nome")]
            public string Nome { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var usuario = await _userManager.GetUserAsync(User);

            if (usuario == null)
            {
                return Challenge();
            }

            Email = usuario.Email ?? string.Empty;
            Input.Nome = usuario.Nome ?? string.Empty;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var usuario = await _userManager.GetUserAsync(User);

            if (usuario == null)
            {
                return Challenge();
            }

            Email = usuario.Email ?? string.Empty;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var novoNome = Input.Nome.Trim();

            if (usuario.Nome == novoNome)
            {
                StatusMessage = "Nenhuma alteração foi necessária.";
                return RedirectToPage();
            }

            usuario.Nome = novoNome;

            var resultado = await _userManager.UpdateAsync(usuario);

            if (!resultado.Succeeded)
            {
                foreach (var erro in resultado.Errors)
                {
                    ModelState.AddModelError(string.Empty, erro.Description);
                }

                return Page();
            }

            await _signInManager.RefreshSignInAsync(usuario);

            StatusMessage = "Seu nome foi atualizado com sucesso.";
            return RedirectToPage();
        }
    }
}