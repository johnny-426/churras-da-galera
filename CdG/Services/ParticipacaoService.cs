using CdG.Data;
using CdG.Models;
using Microsoft.EntityFrameworkCore;

namespace CdG.Services;

// Regras independentes da tela, reutilizáveis em futuros endpoints do aplicativo.
public class ParticipacaoService
{
    private readonly ApplicationDbContext _context;
    private readonly NotificacaoService _notificacoes;

    public ParticipacaoService(ApplicationDbContext context, NotificacaoService notificacoes)
    {
        _context = context;
        _notificacoes = notificacoes;
    }

    public async Task<(bool Sucesso, string Mensagem)> SairAsync(int eventoId, string usuarioId)
    {
        await using var transacao = await _context.Database.BeginTransactionAsync();
        // A mesma linha é bloqueada pelos fluxos de convite, presença e administração.
        var eventos = await _context.Eventos.FromSqlInterpolated($"""
            SELECT * FROM "Eventos" WHERE "Id" = {eventoId} FOR UPDATE
            """).ToListAsync();
        var evento = eventos.SingleOrDefault();
        var vinculo = await _context.VinculosEvento.Ativos()
            .SingleOrDefaultAsync(v => v.EventoId == eventoId && v.UsuarioId == usuarioId);
        if (evento == null || vinculo == null)
            return (false, "Não foi encontrada uma participação ativa neste evento.");
        if (evento.Status != StatusEvento.Agendado && evento.Status != StatusEvento.Adiado)
            return (false, "Este evento está encerrado. Use a lixeira para ocultá-lo do seu histórico.");

        if (await _context.Despesas.AnyAsync(d => d.EventoId == eventoId && d.PagadorId == usuarioId && !d.Anulada))
            return (false, "Você possui despesas registradas. Anule-as antes de sair, ou permaneça até o acerto do evento.");
        var agora = DateTime.UtcNow;
        if (vinculo.Papel == PapelEvento.Organizador)
        {
            var outroOrganizador = await _context.VinculosEvento.Ativos().AnyAsync(v =>
                v.EventoId == eventoId && v.UsuarioId != usuarioId &&
                v.Papel == PapelEvento.Organizador);
            if (!outroOrganizador)
            {
                var candidatos = await _context.VinculosEvento.Ativos()
                    .Where(v => v.EventoId == eventoId && v.UsuarioId != usuarioId &&
                        v.Papel == PapelEvento.Participante).ToListAsync();
                var convites = await _context.Convites.AsNoTracking()
                    .Where(c => c.EventoId == eventoId).ToListAsync();
                // Recupera somente datas documentadas em convites; não inventa antiguidade.
                foreach (var candidato in candidatos)
                {
                    candidato.ParticipacaoAceitaEm ??= convites
                        .Where(c => c.DestinatarioId == candidato.UsuarioId &&
                            c.Status == StatusConvite.Aceito && c.RespondidoEm.HasValue)
                        .Select(c => c.RespondidoEm).Min();
                }
                var pendentes = convites.Where(c => c.Status == StatusConvite.Enviado)
                    .Select(c => c.DestinatarioId).ToHashSet();
                var sucessor = candidatos.Where(v => v.ParticipacaoAceitaEm.HasValue &&
                        !pendentes.Contains(v.UsuarioId))
                    .OrderBy(v => v.ParticipacaoAceitaEm).ThenBy(v => v.Id).FirstOrDefault();
                if (sucessor == null)
                    return (false, "Você é o último organizador e não há participante com aceite registrado para assumir. " +
                        "Convide alguém e aguarde o aceite, promova uma pessoa elegível ou cancele o evento antes de ocultá-lo.");

                sucessor.Papel = PapelEvento.Organizador;
                _notificacoes.Criar(sucessor.UsuarioId, eventoId, TipoNotificacao.PromocaoOrganizador,
                    $"Você assumiu a organização do evento \"{evento.Nome}\" após a saída do último organizador.");
            }
        }

        vinculo.SaiuEm = agora;
        vinculo.Papel = PapelEvento.Participante;
        vinculo.StatusPresenca = StatusPresenca.Cancelada;
        vinculo.PresencaAtualizadaEm = agora;
        // Invalida também formulários de edição abertos antes da saída.
        evento.AtualizadoEm = agora;
        var convitePendente = await _context.Convites.SingleOrDefaultAsync(c =>
            c.EventoId == eventoId && c.DestinatarioId == usuarioId && c.Status == StatusConvite.Enviado);
        if (convitePendente != null)
        {
            convitePendente.Status = StatusConvite.Recusado;
            convitePendente.RespondidoEm = agora;
        }
        var avisos = await _context.Notificacoes.Where(n => n.EventoId == eventoId &&
            n.DestinatarioId == usuarioId && n.LidaEm == null).ToListAsync();
        foreach (var aviso in avisos) aviso.LidaEm = agora;
        await _context.SaveChangesAsync();
        await transacao.CommitAsync();
        return (true, "Você saiu do evento. Sua vaga foi liberada e suas permissões foram encerradas.");
    }

    public async Task<string> DefinirOcultacaoAsync(int eventoId, string usuarioId, bool ocultar)
    {
        await using var transacao = await _context.Database.BeginTransactionAsync();
        var eventos = await _context.Eventos.FromSqlInterpolated($"""
            SELECT * FROM "Eventos" WHERE "Id" = {eventoId} FOR UPDATE
            """).ToListAsync();
        var evento = eventos.SingleOrDefault();
        var vinculo = await _context.VinculosEvento.SingleOrDefaultAsync(v =>
            v.EventoId == eventoId && v.UsuarioId == usuarioId);
        if (evento == null || vinculo == null)
            return "Evento não encontrado no seu histórico.";
        if (evento.Status != StatusEvento.Cancelado && evento.Status != StatusEvento.Concluido)
            return "Somente eventos cancelados ou concluídos podem ser ocultados.";

        vinculo.OcultadoDoHistoricoEm = ocultar ? DateTime.UtcNow : null;
        if (ocultar)
        {
            var avisos = await _context.Notificacoes.Where(n => n.EventoId == eventoId &&
                n.DestinatarioId == usuarioId && n.LidaEm == null).ToListAsync();
            foreach (var aviso in avisos) aviso.LidaEm = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        await transacao.CommitAsync();
        return ocultar ? "Evento ocultado somente do seu histórico. Você pode restaurá-lo em Ocultos."
            : "Evento restaurado no seu histórico. Isso não reativa uma participação encerrada.";
    }
}
