using System.Globalization;
using System.Security.Claims;
using CdG.Data;
using CdG.Models;
using CdG.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace CdG.Pages.Eventos;
[Authorize]
public class FinanceiroModel(ApplicationDbContext db, FinanceiroService financeiro) : PageModel
{
    public Evento Evento { get; set; } = null!;
    public string UsuarioId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public bool Organizador { get; set; }
    public List<Despesa> Despesas { get; set; } = [];
    public List<Acerto> Acertos { get; set; } = [];
    public List<CotaEvento> Cotas { get; set; } = [];
    public Dictionary<string, long> Saldos { get; set; } = [];
    [TempData] public string? Mensagem { get; set; }
    public static string Dinheiro(long centavos) => (centavos / 100m).ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await db.VinculosEvento.AnyAsync(x => x.EventoId == id && x.UsuarioId == UsuarioId && x.SaiuEm == null)) return NotFound();
        Evento = (await db.Eventos.AsNoTracking().SingleAsync(x => x.Id == id));
        Organizador = await PermissoesEvento.EhOrganizadorAsync(db, id, UsuarioId);
        Despesas = await db.Despesas.Include(x => x.Pagador).Where(x => x.EventoId == id).OrderByDescending(x => x.Id).ToListAsync();
        Cotas = await db.CotasEvento.Include(x => x.Usuario).Where(x => x.EventoId == id).OrderBy(x => x.Usuario.Nome).ToListAsync();
        Acertos = await db.Acertos.Include(x => x.De).Include(x => x.Para).Where(x => x.EventoId == id).OrderByDescending(x => x.Id).ToListAsync();
        if (Evento.FinanceiroFechadoEm != null) Saldos = await financeiro.SaldosAsync(id);
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int id, string acao, string? descricao, string? valor, string? destino, int registro)
    {
        long centavos = 0;
        if (acao == "despesa" || acao == "pagar")
        {
            // Aceita 12,50 ou 12.50; não aceita separadores de milhar ambíguos.
            if (!decimal.TryParse(valor?.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantia)
                || quantia <= 0 || quantia > 1000000 || decimal.Round(quantia, 2) != quantia)
            { Mensagem = "Valor inválido. Use até duas casas decimais, sem separador de milhar."; return RedirectToPage(new { id }); }
            centavos = (long)(quantia * 100);
        }
        Mensagem = await financeiro.ExecutarAsync(id, UsuarioId, acao, descricao, centavos, destino, registro);
        return RedirectToPage(new { id });
    }
}
