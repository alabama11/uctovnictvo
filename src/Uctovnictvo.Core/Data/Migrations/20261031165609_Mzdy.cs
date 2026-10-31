using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uctovnictvo.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Mzdy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Zamestnanci",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Meno = table.Column<string>(type: "TEXT", nullable: false),
                    Adresa = table.Column<string>(type: "TEXT", nullable: true),
                    RodneCislo = table.Column<string>(type: "TEXT", nullable: true),
                    Iban = table.Column<string>(type: "TEXT", nullable: true),
                    ZdravotnaPoistovna = table.Column<string>(type: "TEXT", nullable: true),
                    Funkcia = table.Column<string>(type: "TEXT", nullable: true),
                    DatumNastupu = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DatumUkoncenia = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    MesacnaMzda = table.Column<decimal>(type: "TEXT", nullable: false),
                    Vyhlasenie = table.Column<bool>(type: "INTEGER", nullable: false),
                    ZdravotnePostihnuty = table.Column<bool>(type: "INTEGER", nullable: false),
                    DanovyBonusMesacne = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zamestnanci", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zamestnanci_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mzdy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ZamestnanecId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rok = table.Column<int>(type: "INTEGER", nullable: false),
                    Mesiac = table.Column<int>(type: "INTEGER", nullable: false),
                    HrubaMzda = table.Column<decimal>(type: "TEXT", nullable: false),
                    SpZamestnanec = table.Column<decimal>(type: "TEXT", nullable: false),
                    ZpZamestnanec = table.Column<decimal>(type: "TEXT", nullable: false),
                    Nczd = table.Column<decimal>(type: "TEXT", nullable: false),
                    ZakladDane = table.Column<decimal>(type: "TEXT", nullable: false),
                    Preddavok = table.Column<decimal>(type: "TEXT", nullable: false),
                    DanovyBonus = table.Column<decimal>(type: "TEXT", nullable: false),
                    SpZamestnavatel = table.Column<decimal>(type: "TEXT", nullable: false),
                    ZpZamestnavatel = table.Column<decimal>(type: "TEXT", nullable: false),
                    Poznamka = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mzdy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mzdy_Zamestnanci_ZamestnanecId",
                        column: x => x.ZamestnanecId,
                        principalTable: "Zamestnanci",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Mzdy_ZamestnanecId_Rok_Mesiac",
                table: "Mzdy",
                columns: new[] { "ZamestnanecId", "Rok", "Mesiac" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Zamestnanci_JednotkaId",
                table: "Zamestnanci",
                column: "JednotkaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mzdy");

            migrationBuilder.DropTable(
                name: "Zamestnanci");
        }
    }
}
