using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CdG.Migrations
{
    /// <inheritdoc />
    public partial class PreencherOrganizadoresExistentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
        INSERT INTO "VinculosEvento"
            ("EventoId", "UsuarioId", "Papel",
             "StatusPresenca", "PresencaAtualizadaEm")
        SELECT
            e."Id",
            e."CriadorId",
            1,
            CASE
                WHEN e."Status" = 1 THEN 2
                ELSE 1
            END,
            CURRENT_TIMESTAMP
        FROM "Eventos" AS e
        WHERE NOT EXISTS (
            SELECT 1
            FROM "VinculosEvento" AS v
            WHERE v."EventoId" = e."Id"
              AND v."UsuarioId" = e."CriadorId"
        )
        ON CONFLICT ("EventoId", "UsuarioId") DO NOTHING;
        """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Os vínculos preenchidos são preservados em uma reversão.
            // Eles podem ter sido utilizados ou atualizados posteriormente.
        }
    }
}
