using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CdG.Migrations
{
    /// <inheritdoc />
    public partial class PrepararSaidaEHistoricoParticipantes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OcultadoDoHistoricoEm",
                table: "VinculosEvento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ParticipacaoAceitaEm",
                table: "VinculosEvento",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SaiuEm",
                table: "VinculosEvento",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OcultadoDoHistoricoEm",
                table: "VinculosEvento");

            migrationBuilder.DropColumn(
                name: "ParticipacaoAceitaEm",
                table: "VinculosEvento");

            migrationBuilder.DropColumn(
                name: "SaiuEm",
                table: "VinculosEvento");
        }
    }
}
