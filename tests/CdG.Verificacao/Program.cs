using System.Data.Common;
using CdG.Data;
using CdG.Models;
using CdG.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("OK: " + label); }
for (long total = 0; total < 100; total++)
for (int n = 1; n <= 7; n++)
{
    var cotas = CalculoRateio.Dividir(total, Enumerable.Range(1, n).Select(x => x.ToString()));
    if (cotas.Values.Sum() != total || cotas.Values.Max() - cotas.Values.Min() > 1) throw new Exception("Arredondamento");
}
Check(true, "700 rateios preservam soma e diferença máxima de um centavo");
using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).AddInterceptors(new SemForUpdate()).Options;
await using var db = new ApplicationDbContext(options);
await db.Database.EnsureCreatedAsync();
foreach (var id in new[] { "A", "B", "C", "X" }) db.Users.Add(new ApplicationUser { Id = id, Nome = id, UserName = id, NormalizedUserName = id, Email = id + "@example.test" });
var evento = new Evento { Nome = "Teste", Local = "Local", CriadorId = "A", Status = StatusEvento.Agendado };
db.Eventos.Add(evento); await db.SaveChangesAsync();
foreach (var id in new[] { "A", "B", "C" }) db.VinculosEvento.Add(new VinculoEvento { EventoId = evento.Id, UsuarioId = id, Papel = id == "A" ? PapelEvento.Organizador : PapelEvento.Participante, StatusPresenca = StatusPresenca.Confirmada });
await db.SaveChangesAsync();
var svc = new FinanceiroService(db);
var idEvento = evento.Id;
async Task<string> Act(string u, string a, long v = 0, string? to = null, int record = 0) => await svc.ExecutarAsync(idEvento, u, a, "Compra", v, to, record);
Check((await Act("X", "despesa", 100)).Contains("não disponível"), "Estranho não acessa financeiro");
Check((await Act("A", "despesa", -1)).Contains("Informe"), "Valor negativo rejeitado");
await Act("A", "despesa", 10000);
await Act("B", "despesa", 2000);
Check(await db.Despesas.CountAsync() == 2, "Despesas persistidas");
Check((await Act("A", "fechar")).Contains("Conclua"), "Não fecha evento ativo");
Check((await Act("B", "anular", record: db.Despesas.First().Id)).Contains("Somente"), "Não anula gasto alheio");
evento.Status = StatusEvento.Concluido; await db.SaveChangesAsync();
Check((await Act("B", "fechar")).Contains("indisponível"), "Participante não fecha rateio");
await Act("A", "fechar");
Check(await db.CotasEvento.CountAsync() == 3, "Fechamento fixa três cotas");
var saldos = await svc.SaldosAsync(idEvento);
Check(saldos["A"] == 6000 && saldos["B"] == -2000 && saldos["C"] == -4000, "Saldos do exemplo 100 + 20 entre três");
Check((await Act("A", "despesa", 100)).Contains("fechado"), "Despesa bloqueada após fechar");
Check((await Act("A", "fechar")).Contains("indisponível"), "Fechamento duplicado bloqueado");
Check((await Act("B", "pagar", 2001, "A")).Contains("respeitar"), "Pagamento acima da dívida bloqueado");
await Act("B", "pagar", 1000, "A");
var acerto = await db.Acertos.SingleAsync();
Check((await svc.SaldosAsync(idEvento))["B"] == -2000, "Pagamento pendente não altera saldo");
Check((await Act("C", "pagar", 1000, "A")).Contains("pendente"), "Reserva de crédito impede pagamentos conflitantes");
Check((await Act("B", "confirmar", record: acerto.Id)).Contains("recebedor"), "Pagador não confirma recebimento");
Check((await Act("A", "renunciar")).Contains("pendentes"), "Renúncia aguarda acertos pendentes");
await Act("A", "confirmar", record: acerto.Id);
Check((await svc.SaldosAsync(idEvento))["B"] == -1000, "Recebedor confirma pagamento parcial");
Check((await Act("A", "confirmar", record: acerto.Id)).Contains("processado"), "Confirmação repetida não duplica valor");
await Act("C", "pagar", 1000, "A");
var segundo = await db.Acertos.OrderBy(x => x.Id).LastAsync();
await Act("C", "cancelar", record: segundo.Id);
Check((await svc.SaldosAsync(idEvento))["C"] == -4000, "Cancelar registro não muda dívida");
await Act("A", "renunciar");
Check((await svc.SaldosAsync(idEvento)).Values.All(x => x == 0), "Renúncia quita saldo restante e preserva equilíbrio");
Check(await db.Acertos.CountAsync(x => x.Renuncia) == 2, "Renúncia mantém trilha por beneficiário");
Console.WriteLine($"{checks} verificações concluídas. SQLite não valida bloqueios/concorrrência do PostgreSQL.");

// Apenas no teste SQLite: PostgreSQL real mantém FOR UPDATE em todas as operações.
class SemForUpdate : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { command.CommandText = command.CommandText.Replace(" FOR UPDATE", ""); return ValueTask.FromResult(result); }
}
