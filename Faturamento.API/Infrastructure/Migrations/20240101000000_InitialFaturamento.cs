using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Faturamento.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialFaturamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cria a SEQUENCE de negócio para numeração das notas
            migrationBuilder.Sql(
                "CREATE SEQUENCE dbo.NumeroNotaSequence AS INT START WITH 1 INCREMENT BY 1");

            migrationBuilder.CreateTable(
                name: "NotasFiscais",
                columns: table => new
                {
                    // Id = IDENTITY (chave técnica, PK)
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),

                    // Numero = valor da SEQUENCE (número de negócio, único, sequencial)
                    Numero = table.Column<int>(type: "int", nullable: false,
                        defaultValueSql: "NEXT VALUE FOR dbo.NumeroNotaSequence"),

                    DataEmissao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status      = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValorTotal  = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasFiscais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensNota",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotaFiscalId     = table.Column<int>(type: "int", nullable: false),
                    CodigoProduto    = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DescricaoProduto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantidade       = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ValorUnitario    = table.Column<decimal>(type: "decimal(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensNota", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensNota_NotasFiscais_NotaFiscalId",
                        column: x => x.NotaFiscalId,
                        principalTable: "NotasFiscais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensNota_NotaFiscalId",
                table: "ItensNota",
                column: "NotaFiscalId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_Numero",
                table: "NotasFiscais",
                column: "Numero",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ItensNota");
            migrationBuilder.DropTable(name: "NotasFiscais");
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS dbo.NumeroNotaSequence");
        }
    }
}
