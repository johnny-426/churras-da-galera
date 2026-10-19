using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CdG.Migrations
{
    /// <inheritdoc />
    public partial class PreencherAceitesAntigos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O criador aceitou participar quando criou o evento.
            migrationBuilder.Sql("""
        UPDATE "VinculosEvento" AS v
        SET "ParticipacaoAceitaEm" = e."CriadoEm"
        FROM "Eventos" AS e
        WHERE v."EventoId" = e."Id"
          AND v."UsuarioId" = e."CriadorId"
          AND v."ParticipacaoAceitaEm" IS NULL;
        """);

            // Recupera o aceite registrado nos convites antigos.
            migrationBuilder.Sql("""
        UPDATE "VinculosEvento" AS v
        SET "ParticipacaoAceitaEm" = aceites."AceitoEm"
        FROM (
            SELECT
                "EventoId",
                "DestinatarioId",
                MIN("RespondidoEm") AS "AceitoEm"
            FROM "Convites"
            WHERE "Status" = 2
              AND "RespondidoEm" IS NOT NULL
            GROUP BY "EventoId", "DestinatarioId"
        ) AS aceites
        WHERE v."EventoId" = aceites."EventoId"
          AND v."UsuarioId" = aceites."DestinatarioId"
          AND v."ParticipacaoAceitaEm" IS NULL;
        """);

            // Para quem confirmou pelos detalhes, usa a confirmação
            // ainda registrada, caso não exista um aceite recuperável.
            migrationBuilder.Sql("""
        UPDATE "VinculosEvento"
        SET "ParticipacaoAceitaEm" = "PresencaAtualizadaEm"
        WHERE "ParticipacaoAceitaEm" IS NULL
          AND "StatusPresenca" = 2;
        """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "O preenchimento dos aceites antigos não pode ser " +
                "desfeito automaticamente sem apagar informações válidas.");
        }
    }
}
