using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CdG.Models
{
    public class Notificacao
    {
        public int Id { get; set; }

        [Required]
        public string DestinatarioId { get; set; } = string.Empty;

        public ApplicationUser Destinatario { get; set; } = null!;

        public int EventoId { get; set; }

        public Evento Evento { get; set; } = null!;

        public TipoNotificacao Tipo { get; set; }

        [Required]
        [StringLength(1000)]
        public string Mensagem { get; set; } = string.Empty;

        public DateTime CriadaEm { get; set; } = DateTime.UtcNow;

        public DateTime? LidaEm { get; set; }
    }
}