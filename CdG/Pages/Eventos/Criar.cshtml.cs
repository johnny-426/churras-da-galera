using System.ComponentModel.DataAnnotations;
using CdG.Data;
using CdG.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CdG.Pages.Eventos
{
    [Authorize]
    public class CriarModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CriarModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public DadosEvento Entrada { get; set; } = new();

        [TempData]
        public string? Mensagem { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var usuario = await _userManager.GetUserAsync(User);

            if (usuario == null)
                return Challenge();

            var fuso = TimeZoneInfo.FindSystemTimeZoneById(
                "E. South America Standard Time");

            var horarioInformado = DateTime.SpecifyKind(
                Entrada.DataHora!.Value,
                DateTimeKind.Unspecified);

            if (fuso.IsInvalidTime(horarioInformado))
            {
                ModelState.AddModelError(
                    "Entrada.DataHora", "Informe um horário válido.");

                return Page();
            }

            var horarioUtc = TimeZoneInfo.ConvertTimeToUtc(
                horarioInformado, fuso);

            if (horarioUtc <= DateTime.UtcNow)
            {
                ModelState.AddModelError(
                    "Entrada.DataHora",
                    "Informe uma data e um horário futuros.");

                return Page();
            }

            var agora = DateTime.UtcNow;

            var evento = new Evento
            {
                Nome = Entrada.Nome.Trim(),
                Descricao = Entrada.Descricao?.Trim(),
                DataHora = horarioUtc,
                Local = Entrada.Local.Trim(),
                LimiteParticipantes = Entrada.LimiteParticipantes,
                CriadorId = usuario.Id,
                Status = StatusEvento.Agendado,
                CriadoEm = agora,
                AtualizadoEm = agora
            };

            var vinculoOrganizador = new VinculoEvento
            {
                Evento = evento,
                UsuarioId = usuario.Id,
                Papel = PapelEvento.Organizador,
                StatusPresenca = StatusPresenca.Confirmada,
                PresencaAtualizadaEm = agora,
                ParticipacaoAceitaEm = agora
            };

            _context.VinculosEvento.Add(vinculoOrganizador);

            await _context.SaveChangesAsync();

            Mensagem = "Evento criado com sucesso!";

            return RedirectToPage("/Eventos/Index");
        }

        public class DadosEvento
        {
            [Required(ErrorMessage = "Informe o nome do evento.")]
            [StringLength(100)]
            public string Nome { get; set; } = string.Empty;

            [StringLength(2000)]
            public string? Descricao { get; set; }

            [Required(ErrorMessage = "Informe a data e o horário.")]
            public DateTime? DataHora { get; set; }

            [Required(ErrorMessage = "Informe o local.")]
            [StringLength(300)]
            public string Local { get; set; } = string.Empty;

            [Range(1, int.MaxValue,
                ErrorMessage = "O limite deve ser maior que zero.")]
            public int? LimiteParticipantes { get; set; }
        }
    }
}