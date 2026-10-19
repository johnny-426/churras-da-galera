using System.ComponentModel.DataAnnotations;
namespace CdG.Models;

// Valores em centavos: evita diferenças de arredondamento no banco e no rateio.
public class Despesa
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public Evento Evento { get; set; } = null!;
    [MaxLength(200)] public string Descricao { get; set; } = "";
    public string PagadorId { get; set; } = "";
    public ApplicationUser Pagador { get; set; } = null!;
    public long Centavos { get; set; }
    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
    public bool Anulada { get; set; }
}
public class CotaEvento
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public Evento Evento { get; set; } = null!;
    public string UsuarioId { get; set; } = "";
    public ApplicationUser Usuario { get; set; } = null!;
    public long Centavos { get; set; }
}
public class Acerto
{
    public int Id { get; set; }
    public int EventoId { get; set; }
    public Evento Evento { get; set; } = null!;
    public string DeId { get; set; } = "";
    public ApplicationUser De { get; set; } = null!;
    public string ParaId { get; set; } = "";
    public ApplicationUser Para { get; set; } = null!;
    public long Centavos { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmadoEm { get; set; }
    public bool Cancelado { get; set; }
    public bool Renuncia { get; set; }
}
