using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CdG.Models
{
    public class Convite
    {
        public int Id { get; set; }

        public int EventoId { get; set; }

        public Evento Evento { get; set; } = null!;

        [Required]
        public string DestinatarioId { get; set; } = string.Empty;

        public ApplicationUser Destinatario { get; set; } = null!;

        public StatusConvite Status { get; set; }
            = StatusConvite.Enviado;

        public DateTime EnviadoEm { get; set; } = DateTime.UtcNow;

        public DateTime? RespondidoEm { get; set; }
    }
}