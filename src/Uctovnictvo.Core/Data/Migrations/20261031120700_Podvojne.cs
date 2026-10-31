using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uctovnictvo.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Podvojne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Ucet",
                table: "KategoriePohybov",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ucet",
                table: "Faktury",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UctovneZapisy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Doklad = table.Column<string>(type: "TEXT", nullable: false),
                    Popis = table.Column<string>(type: "TEXT", nullable: false),
                    Md = table.Column<string>(type: "TEXT", nullable: false),
                    D = table.Column<string>(type: "TEXT", nullable: false),
                    Suma = table.Column<decimal>(type: "TEXT", nullable: false),
                    Zdroj = table.Column<int>(type: "INTEGER", nullable: false),
                    FakturaId = table.Column<int>(type: "INTEGER", nullable: true),
                    Partner = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UctovneZapisy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UctovneZapisy_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ucty",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Cislo = table.Column<string>(type: "TEXT", nullable: false),
                    Nazov = table.Column<string>(type: "TEXT", nullable: false),
                    Typ = table.Column<int>(type: "INTEGER", nullable: false),
                    Danovy = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ucty", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ucty_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UctovneZapisy_JednotkaId_Datum",
                table: "UctovneZapisy",
                columns: new[] { "JednotkaId", "Datum" });

            migrationBuilder.CreateIndex(
                name: "IX_Ucty_JednotkaId_Cislo",
                table: "Ucty",
                columns: new[] { "JednotkaId", "Cislo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UctovneZapisy");

            migrationBuilder.DropTable(
                name: "Ucty");

            migrationBuilder.DropColumn(
                name: "Ucet",
                table: "KategoriePohybov");

            migrationBuilder.DropColumn(
                name: "Ucet",
                table: "Faktury");
        }
    }
}
