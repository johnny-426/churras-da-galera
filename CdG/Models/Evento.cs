using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CdG.Models
{
    public class Evento
    {
        public int Id { get; set; }
        public DateTime? FinanceiroFechadoEm { get; set; }

        [Required]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Descricao { get; set; }

        public DateTime? DataHora { get; set; }

        [Required]
        [StringLength(300)]
        public string Local { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int? LimiteParticipantes { get; set; }

        public StatusEvento Status { get; set; }
            = StatusEvento.Agendado;

        [Required]
        public string CriadorId { get; set; } = string.Empty;

        public ApplicationUser Criador { get; set; } = null!;

        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    }
}