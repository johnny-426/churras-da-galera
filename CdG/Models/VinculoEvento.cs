using System.ComponentModel.DataAnnotations;

namespace CdG.Models
{
    public class VinculoEvento
    {
        public int Id { get; set; }

        public int EventoId { get; set; }

        public Evento Evento { get; set; } = null!;

        [Required]
        public string UsuarioId { get; set; } = string.Empty;

        public ApplicationUser Usuario { get; set; } = null!;

        public PapelEvento Papel { get; set; }
            = PapelEvento.Participante;

        public StatusPresenca StatusPresenca { get; set; }
            = global::CdG.Models.StatusPresenca.Pendente;

        public DateTime PresencaAtualizadaEm { get; set; }
            = DateTime.UtcNow;

        public DateTime? ParticipacaoAceitaEm { get; set; }

        public DateTime? SaiuEm { get; set; }

        public DateTime? OcultadoDoHistoricoEm { get; set; }
    }
}