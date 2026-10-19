using CdG.Data;
using CdG.Models;
using CdG.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CdG.Pages.Eventos
{
    [Authorize]
    public class DetalhesModel : PageModel
    {
        private readonly NotificacaoService _notificacoes;
        private readonly ParticipacaoService _participacoes;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public HashSet<string> UsuariosElegiveisParaPromocao { get; private set; }
    = new();
        public DetalhesModel(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    NotificacaoService notificacoes, ParticipacaoService participacoes)
        {
            _context = context;
            _participacoes = participacoes;
            _userManager = userManager;
            _notificacoes = notificacoes;
        }

        public StatusPresenca? MinhaPresenca { get; private set; }

        public bool ConvitePendente { get; private set; }

        [TempData]
        public string? Mensagem { get; set; }

        public bool EhOrganizador { get; private set; }

        public Evento Evento { get; private set; } = null!;

        public string DataFormatada { get; private set; }
            = "Data a definir";

        public List<VinculoEvento> Participantes { get; private set; }
        = new();

        public int TotalConfirmados =>
            Participantes.Count(vinculo =>
                vinculo.StatusPresenca == StatusPresenca.Confirmada);

        public int TotalPendentes =>
            Participantes.Count(vinculo =>
                vinculo.StatusPresenca == StatusPresenca.Pendente);
        public async Task<IActionResult> OnGetAsync(int id)

        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            var evento = await _context.Eventos
    .AsNoTracking()
    .FirstOrDefaultAsync(evento =>
        evento.Id == id &&
        _context.VinculosEvento.Any(v =>
            v.EventoId == evento.Id &&
            v.UsuarioId == usuarioId &&
            v.SaiuEm == null &&
            v.OcultadoDoHistoricoEm == null));

            if (evento == null)
                return NotFound();

            Evento = evento;
            EhOrganizador = await PermissoesEvento.EhOrganizadorAsync(
    _context, evento.Id, usuarioId);

            MinhaPresenca = await _context.VinculosEvento.Ativos()
    .AsNoTracking()
    .Where(vinculo =>
        vinculo.EventoId == evento.Id &&
        vinculo.UsuarioId == usuarioId)
    .Select(vinculo => (StatusPresenca?)vinculo.StatusPresenca)
    .SingleOrDefaultAsync();

            ConvitePendente = await _context.Convites
                .AsNoTracking()
                .AnyAsync(convite =>
                    convite.EventoId == evento.Id &&
                    convite.DestinatarioId == usuarioId &&
                    convite.Status == StatusConvite.Enviado);

            if (EhOrganizador)
            {
                Participantes = await _context.VinculosEvento.Ativos()
                    .AsNoTracking()
                    .Include(vinculo => vinculo.Usuario)
                    .Where(vinculo =>
                        vinculo.EventoId == evento.Id &&
                        vinculo.SaiuEm == null &&
                        vinculo.OcultadoDoHistoricoEm == null)
                    .OrderBy(vinculo => vinculo.Papel)
                    .ThenBy(vinculo => vinculo.Usuario.Nome)
                    .ThenBy(vinculo => vinculo.Id)
                    .ToListAsync();
                var usuariosComConviteAceito = await _context.Convites
                    .AsNoTracking()
                    .Where(c =>
                    c.EventoId == evento.Id &&
                    c.Status == StatusConvite.Aceito)
                    .Select(c => c.DestinatarioId)
                    .ToListAsync();

                UsuariosElegiveisParaPromocao = Participantes
                    .Where(v =>
                        v.Papel == PapelEvento.Participante &&
                        (v.ParticipacaoAceitaEm.HasValue ||
                         usuariosComConviteAceito.Contains(v.UsuarioId)))
                    .Select(v => v.UsuarioId)
                    .ToHashSet();
            }

            if (evento.DataHora.HasValue)
            {
                var fuso = TimeZoneInfo.FindSystemTimeZoneById(
                    "E. South America Standard Time");

                DataFormatada = TimeZoneInfo
                    .ConvertTimeFromUtc(evento.DataHora.Value, fuso)
                    .ToString("dd/MM/yyyy 'às' HH:mm");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostPromoverAsync(
    int id, int vinculoId)
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            await using var transacao =
                await _context.Database.BeginTransactionAsync();

            var eventos = await _context.Eventos
                .FromSqlInterpolated($"""
            SELECT *
            FROM "Eventos"
            WHERE "Id" = {id}
            FOR UPDATE
            """)
                .ToListAsync();

            var evento = eventos.SingleOrDefault();

            if (evento == null)
                return NotFound();

            if (!await PermissoesEvento.EhOrganizadorAsync(
                    _context, evento.Id, usuarioId))
            {
                return NotFound();
            }

            if (evento.Status != StatusEvento.Agendado &&
                evento.Status != StatusEvento.Adiado)
            {
                Mensagem = "Eventos encerrados não permitem novos organizadores.";
                return RedirectToPage(new { id });
            }

            var participante = await _context.VinculosEvento.Ativos()
                .SingleOrDefaultAsync(v =>
                    v.Id == vinculoId &&
                    v.EventoId == evento.Id &&
                    v.SaiuEm == null &&
                    v.OcultadoDoHistoricoEm == null);

            if (participante == null)
                return NotFound();

            if (participante.Papel == PapelEvento.Organizador)
            {
                Mensagem = "Essa pessoa já é organizadora.";
                return RedirectToPage(new { id });
            }

            var aceitouConvite = await _context.Convites
                .AnyAsync(c =>
                    c.EventoId == evento.Id &&
                    c.DestinatarioId == participante.UsuarioId &&
                    c.Status == StatusConvite.Aceito);

            if (!participante.ParticipacaoAceitaEm.HasValue &&
                !aceitouConvite)
            {
                Mensagem = "Essa pessoa precisa aceitar participar antes da promoção.";
                return RedirectToPage(new { id });
            }

            participante.Papel = PapelEvento.Organizador;
            evento.AtualizadoEm = DateTime.UtcNow;

            _notificacoes.Criar(
                participante.UsuarioId,
                evento.Id,
                TipoNotificacao.PromocaoOrganizador,
                $"Você se tornou organizador do evento \"{evento.Nome}\". " +
                "Agora pode editar, reagendar, cancelar e promover participantes.");

            await _context.SaveChangesAsync();
            await transacao.CommitAsync();

            Mensagem = "Participante promovido a organizador.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostSairAsync(int id)
        {
            var usuarioId = _userManager.GetUserId(User);
            if (usuarioId == null) return Challenge();
            var resultado = await _participacoes.SairAsync(id, usuarioId);
            Mensagem = resultado.Mensagem;
            return resultado.Sucesso ? RedirectToPage("/Eventos/Index") : RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostCancelarAsync(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            await using var transacao =
    await _context.Database.BeginTransactionAsync();

            var eventos = await _context.Eventos
    .FromSqlInterpolated($"""
        SELECT *
        FROM "Eventos"
        WHERE "Id" = {id}
        FOR UPDATE
        """)
    .ToListAsync();

            var evento = eventos.SingleOrDefault();

            if (evento == null)
                return NotFound();

            if (!await PermissoesEvento.EhOrganizadorAsync(
        _context, evento.Id, usuarioId))
            {
                return NotFound();
            }

            if (evento.Status != StatusEvento.Agendado &&
                evento.Status != StatusEvento.Adiado)
            {
                TempData["Mensagem"] =
                    "Esse evento já foi cancelado ou concluído.";

                return RedirectToPage("/Eventos/Index");
            }

            evento.Status = StatusEvento.Cancelado;
            evento.AtualizadoEm = DateTime.UtcNow;

            await _notificacoes.AvisarParticipantesAsync(
    evento.Id,
    usuarioId,
    TipoNotificacao.CancelamentoEvento,
    $"O evento \"{evento.Nome}\" foi cancelado. " +
    "Seus registros continuam disponíveis para consulta.");

            await _context.SaveChangesAsync();
            await transacao.CommitAsync();

            TempData["Mensagem"] = "Evento cancelado com sucesso.";

            return RedirectToPage("/Eventos/Index");
        }
        public async Task<IActionResult> OnPostConcluirAsync(int id)
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            await using var transacao =
    await _context.Database.BeginTransactionAsync();

            var eventos = await _context.Eventos
    .FromSqlInterpolated($"""
        SELECT *
        FROM "Eventos"
        WHERE "Id" = {id}
        FOR UPDATE
        """)
    .ToListAsync();

            var evento = eventos.SingleOrDefault();

            if (evento == null)
                return NotFound();

            if (!await PermissoesEvento.EhOrganizadorAsync(
        _context, evento.Id, usuarioId))
            {
                return NotFound();
            }

            if (evento.Status != StatusEvento.Agendado ||
                !evento.DataHora.HasValue ||
                evento.DataHora.Value > DateTime.UtcNow)
            {
                TempData["Mensagem"] =
                    "Só é possível concluir um evento agendado cujo horário já chegou.";

                return RedirectToPage("/Eventos/Index");
            }

            evento.Status = StatusEvento.Concluido;
            evento.AtualizadoEm = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transacao.CommitAsync();

            TempData["Mensagem"] = "Evento concluído com sucesso.";

            return RedirectToPage("/Eventos/Index");
        }

        public async Task<IActionResult> OnPostPresencaAsync(
    int id, string acao, long? dataTicks)
        {
            var usuarioId = _userManager.GetUserId(User);

            if (usuarioId == null)
                return Challenge();

            if (acao != "confirmar" && acao != "cancelar")
                return BadRequest();

            await using var transacao =
                await _context.Database.BeginTransactionAsync();

            var eventos = await _context.Eventos
                .FromSqlInterpolated($"""
            SELECT *
            FROM "Eventos"
            WHERE "Id" = {id}
            FOR UPDATE
            """)
                .ToListAsync();

            var evento = eventos.SingleOrDefault();

            if (evento == null)
                return NotFound();

            var vinculo = await _context.VinculosEvento.Ativos()
                .SingleOrDefaultAsync(vinculo =>
                    vinculo.EventoId == evento.Id &&
                    vinculo.UsuarioId == usuarioId);

            if (vinculo == null)
                return NotFound();

            if (evento.Status != StatusEvento.Agendado ||
                !evento.DataHora.HasValue ||
                evento.DataHora.Value <= DateTime.UtcNow)
            {
                Mensagem =
                    "A presença só pode ser alterada em eventos agendados com data futura.";

                return RedirectToPage(new { id });
            }

            if (dataTicks != evento.DataHora.Value.Ticks)
            {
                Mensagem =
                    "A data mudou. Confira o novo horário antes de responder.";

                return RedirectToPage(new { id });
            }

            var convitePendente = await _context.Convites
                .AnyAsync(convite =>
                    convite.EventoId == evento.Id &&
                    convite.DestinatarioId == usuarioId &&
                    convite.Status == StatusConvite.Enviado);

            if (convitePendente)
            {
                Mensagem =
                    "Responda primeiro ao convite pela tela Meus convites.";

                return RedirectToPage(new { id });
            }

            if (acao == "cancelar" && await _context.Despesas.AnyAsync(d => d.EventoId == id && d.PagadorId == usuarioId && !d.Anulada))
            { Mensagem = "Anule suas despesas antes de cancelar a presença."; return RedirectToPage(new { id }); }
            var confirmar = acao == "confirmar";

            if (confirmar &&
                vinculo.StatusPresenca != StatusPresenca.Confirmada &&
                evento.LimiteParticipantes.HasValue)
            {
                var confirmados = await _context.VinculosEvento.Ativos()
                    .CountAsync(participacao =>
                        participacao.EventoId == evento.Id &&
                        participacao.StatusPresenca == StatusPresenca.Confirmada);

                if (confirmados >= evento.LimiteParticipantes.Value)
                {
                    Mensagem = "O evento está lotado. Sua presença não foi alterada.";

                    return RedirectToPage(new { id });
                }
            }

            var novoStatus = confirmar
                ? StatusPresenca.Confirmada
                : StatusPresenca.Cancelada;

            if (vinculo.StatusPresenca == novoStatus)
            {
                Mensagem = "Sua presença já está nessa situação.";
                return RedirectToPage(new { id });
            }

            vinculo.StatusPresenca = novoStatus;
            vinculo.PresencaAtualizadaEm = DateTime.UtcNow;

            if (confirmar)
            {
                vinculo.ParticipacaoAceitaEm ??= vinculo.PresencaAtualizadaEm;
            }

            await _context.SaveChangesAsync();
            await transacao.CommitAsync();

            Mensagem = confirmar
                ? "Sua presença foi confirmada."
                : "Sua presença foi cancelada.";

            return RedirectToPage(new { id });
        }
    }
}
