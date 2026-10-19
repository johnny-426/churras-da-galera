using CdG.Data;
using CdG.Models;
using Microsoft.EntityFrameworkCore;
namespace CdG.Services;

public class FinanceiroService(ApplicationDbContext db)
{
    public async Task<Dictionary<string, long>> SaldosAsync(int id)
    {
        var cotas = await db.CotasEvento.Where(x => x.EventoId == id).ToListAsync();
        var saldos = cotas.ToDictionary(x => x.UsuarioId, x => -x.Centavos);
        foreach (var d in await db.Despesas.Where(x => x.EventoId == id && !x.Anulada).ToListAsync())
            saldos[d.PagadorId] = saldos.GetValueOrDefault(d.PagadorId) + d.Centavos;
        foreach (var a in await db.Acertos.Where(x => x.EventoId == id && !x.Cancelado && x.ConfirmadoEm != null).ToListAsync())
        { saldos[a.DeId] += a.Centavos; saldos[a.ParaId] -= a.Centavos; }
        return saldos;
    }

    // Toda alteração usa o mesmo bloqueio das páginas de eventos e participação.
    public async Task<string> ExecutarAsync(int id, string usuario, string acao,
        string? descricao = null, long valor = 0, string? destino = null, int registro = 0)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var eventos = await db.Eventos.FromSqlInterpolated($"SELECT * FROM \"Eventos\" WHERE \"Id\" = {id} FOR UPDATE").ToListAsync();
        var e = eventos.SingleOrDefault();
        if (e == null || !await db.VinculosEvento.AnyAsync(x => x.EventoId == id && x.UsuarioId == usuario && x.SaiuEm == null))
            return "Evento não disponível.";
        var organizador = await PermissoesEvento.EhOrganizadorAsync(db, id, usuario);
        bool fechado = e.FinanceiroFechadoEm != null;
        if (acao == "despesa")
        {
            if (fechado) return "O rateio já foi fechado.";
            if (valor <= 0 || valor > 100000000 || string.IsNullOrWhiteSpace(descricao) || descricao.Length > 200)
                return "Informe descrição e valor entre R$ 0,01 e R$ 1.000.000,00.";
            if (!await db.VinculosEvento.AnyAsync(x => x.EventoId == id && x.UsuarioId == usuario && x.SaiuEm == null && x.StatusPresenca == StatusPresenca.Confirmada))
                return "Apenas participantes confirmados podem registrar despesas próprias.";
            db.Despesas.Add(new Despesa { EventoId = id, PagadorId = usuario, Descricao = descricao.Trim(), Centavos = valor });
        }
        else if (acao == "anular")
        {
            if (fechado) return "O rateio já foi fechado.";
            var d = await db.Despesas.SingleOrDefaultAsync(x => x.Id == registro && x.EventoId == id);
            if (d == null || d.PagadorId != usuario) return "Somente quem registrou pode anular a despesa.";
            d.Anulada = true;
        }
        else if (acao == "fechar")
        {
            if (!organizador || fechado) return "Fechamento indisponível.";
            if (e.Status != StatusEvento.Concluido && e.Status != StatusEvento.Cancelado)
                return "Conclua ou cancele o evento antes de fechar o rateio.";
            var usuarios = await db.VinculosEvento.Where(x => x.EventoId == id && x.SaiuEm == null && x.StatusPresenca == StatusPresenca.Confirmada).Select(x => x.UsuarioId).ToListAsync();
            if (usuarios.Count == 0) return "Não há participantes confirmados para o rateio.";
            var despesas = await db.Despesas.Where(x => x.EventoId == id && !x.Anulada).ToListAsync();
            if (despesas.Any(x => !usuarios.Contains(x.PagadorId))) return "Há despesa de pessoa não confirmada. O autor deve anulá-la antes do fechamento.";
            foreach (var c in CalculoRateio.Dividir(despesas.Sum(x => x.Centavos), usuarios))
                db.CotasEvento.Add(new CotaEvento { EventoId = id, UsuarioId = c.Key, Centavos = c.Value });
            e.FinanceiroFechadoEm = DateTime.UtcNow;
        }
        else
        {
            if (!fechado) return "Feche o rateio antes de registrar acertos.";
            var saldos = await SaldosAsync(id);
            if (acao == "pagar")
            {
                if (destino == null || destino == usuario || valor <= 0 || valor > -saldos.GetValueOrDefault(usuario) || valor > saldos.GetValueOrDefault(destino))
                    return "O valor deve respeitar sua dívida e o crédito de quem recebe.";
                if (await db.Acertos.AnyAsync(x => x.EventoId == id && !x.Cancelado && x.ConfirmadoEm == null && (x.DeId == usuario || x.ParaId == destino)))
                    return "Há um acerto pendente envolvendo esse devedor ou credor. Aguarde confirmação ou cancele o registro.";
                db.Acertos.Add(new Acerto { EventoId = id, DeId = usuario, ParaId = destino, Centavos = valor });
            }
            else if (acao == "confirmar" || acao == "cancelar")
            {
                var a = await db.Acertos.SingleOrDefaultAsync(x => x.Id == registro && x.EventoId == id);
                if (a == null || a.Cancelado || a.ConfirmadoEm != null) return "Acerto já processado ou inexistente.";
                if (acao == "confirmar")
                {
                    if (a.ParaId != usuario) return "Só o recebedor pode confirmar.";
                    if (a.Centavos > -saldos.GetValueOrDefault(a.DeId) || a.Centavos > saldos.GetValueOrDefault(a.ParaId)) return "Saldo insuficiente. Cancele este acerto.";
                    a.ConfirmadoEm = DateTime.UtcNow;
                }
                else
                {
                    if (a.DeId != usuario && a.ParaId != usuario) return "Sem permissão.";
                    a.Cancelado = true;
                }
            }
            else if (acao == "renunciar")
            {
                long credito = saldos.GetValueOrDefault(usuario);
                if (credito <= 0) return "Você não possui crédito para renunciar.";
                if (await db.Acertos.AnyAsync(x => x.EventoId == id && !x.Cancelado && x.ConfirmadoEm == null)) return "Resolva os acertos pendentes antes da renúncia.";
                // Beneficia devedores em ordem estável; mantém histórico e soma dos saldos zero.
                foreach (var devedor in saldos.Where(x => x.Value < 0).OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    long quantia = Math.Min(credito, -devedor.Value);
                    if (quantia == 0) break;
                    db.Acertos.Add(new Acerto { EventoId = id, DeId = devedor.Key, ParaId = usuario, Centavos = quantia, Renuncia = true, ConfirmadoEm = DateTime.UtcNow });
                    credito -= quantia;
                }
            }
            else return "Ação desconhecida.";
        }
        e.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return "Operação registrada com sucesso.";
    }
}
