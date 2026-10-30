using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uctovnictvo.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jednotky",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Typ = table.Column<int>(type: "INTEGER", nullable: false),
                    Nazov = table.Column<string>(type: "TEXT", nullable: false),
                    Ico = table.Column<string>(type: "TEXT", nullable: true),
                    Dic = table.Column<string>(type: "TEXT", nullable: true),
                    Ulica = table.Column<string>(type: "TEXT", nullable: true),
                    Obec = table.Column<string>(type: "TEXT", nullable: true),
                    Psc = table.Column<string>(type: "TEXT", nullable: true),
                    Iban = table.Column<string>(type: "TEXT", nullable: true),
                    IbanFondOprav = table.Column<string>(type: "TEXT", nullable: true),
                    Statutar = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Telefon = table.Column<string>(type: "TEXT", nullable: true),
                    Poznamka = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jednotky", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KategorieNakladov",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nazov = table.Column<string>(type: "TEXT", nullable: false),
                    Kluc = table.Column<int>(type: "INTEGER", nullable: false),
                    JeFondOprav = table.Column<bool>(type: "INTEGER", nullable: false),
                    LenPriestory = table.Column<string>(type: "TEXT", nullable: true),
                    Poradie = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KategorieNakladov", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KategorieNakladov_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Priestory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Cislo = table.Column<string>(type: "TEXT", nullable: false),
                    Typ = table.Column<int>(type: "INTEGER", nullable: false),
                    Vchod = table.Column<string>(type: "TEXT", nullable: true),
                    Poschodie = table.Column<string>(type: "TEXT", nullable: true),
                    Plocha = table.Column<decimal>(type: "TEXT", nullable: false),
                    PlochaBalkona = table.Column<decimal>(type: "TEXT", nullable: false),
                    PocetOsob = table.Column<int>(type: "INTEGER", nullable: false),
                    PodielCitatel = table.Column<int>(type: "INTEGER", nullable: false),
                    PodielMenovatel = table.Column<int>(type: "INTEGER", nullable: false),
                    VariabilnySymbol = table.Column<string>(type: "TEXT", nullable: true),
                    CisloVodomeru = table.Column<string>(type: "TEXT", nullable: true),
                    Poznamka = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Priestory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Priestory_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NakladyRoka",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KategoriaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rok = table.Column<int>(type: "INTEGER", nullable: false),
                    Suma = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NakladyRoka", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NakladyRoka_KategorieNakladov_KategoriaId",
                        column: x => x.KategoriaId,
                        principalTable: "KategorieNakladov",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Predpisy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PriestorId = table.Column<int>(type: "INTEGER", nullable: false),
                    KategoriaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rok = table.Column<int>(type: "INTEGER", nullable: false),
                    RocnaSuma = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predpisy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Predpisy_KategorieNakladov_KategoriaId",
                        column: x => x.KategoriaId,
                        principalTable: "KategorieNakladov",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Predpisy_Priestory_PriestorId",
                        column: x => x.PriestorId,
                        principalTable: "Priestory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriestorRoky",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PriestorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rok = table.Column<int>(type: "INTEGER", nullable: false),
                    DatumOdpoctuZaciatok = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    StavVodomeruZaciatok = table.Column<decimal>(type: "TEXT", nullable: true),
                    DatumOdpoctuKoniec = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    StavVodomeruKoniec = table.Column<decimal>(type: "TEXT", nullable: true),
                    PoistnePredpis = table.Column<decimal>(type: "TEXT", nullable: false),
                    ZostatokMinulyRok = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriestorRoky", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriestorRoky_Priestory_PriestorId",
                        column: x => x.PriestorId,
                        principalTable: "Priestory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Uhrady",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PriestorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Suma = table.Column<decimal>(type: "TEXT", nullable: false),
                    Ucel = table.Column<int>(type: "INTEGER", nullable: false),
                    IbanPlatitela = table.Column<string>(type: "TEXT", nullable: true),
                    VariabilnySymbol = table.Column<string>(type: "TEXT", nullable: true),
                    Poznamka = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uhrady", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Uhrady_Priestory_PriestorId",
                        column: x => x.PriestorId,
                        principalTable: "Priestory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vlastnici",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PriestorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Meno = table.Column<string>(type: "TEXT", nullable: false),
                    Ulica = table.Column<string>(type: "TEXT", nullable: true),
                    Obec = table.Column<string>(type: "TEXT", nullable: true),
                    Psc = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Telefon = table.Column<string>(type: "TEXT", nullable: true),
                    Hlavny = table.Column<bool>(type: "INTEGER", nullable: false),
                    Poznamka = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vlastnici", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vlastnici_Priestory_PriestorId",
                        column: x => x.PriestorId,
                        principalTable: "Priestory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankoveUcty",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VlastnikId = table.Column<int>(type: "INTEGER", nullable: false),
                    Iban = table.Column<string>(type: "TEXT", nullable: false),
                    Poznamka = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankoveUcty", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankoveUcty_Vlastnici_VlastnikId",
                        column: x => x.VlastnikId,
                        principalTable: "Vlastnici",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankoveUcty_Iban",
                table: "BankoveUcty",
                column: "Iban");

            migrationBuilder.CreateIndex(
                name: "IX_BankoveUcty_VlastnikId",
                table: "BankoveUcty",
                column: "VlastnikId");

            migrationBuilder.CreateIndex(
                name: "IX_KategorieNakladov_JednotkaId",
                table: "KategorieNakladov",
                column: "JednotkaId");

            migrationBuilder.CreateIndex(
                name: "IX_NakladyRoka_KategoriaId_Rok",
                table: "NakladyRoka",
                columns: new[] { "KategoriaId", "Rok" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predpisy_KategoriaId",
                table: "Predpisy",
                column: "KategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Predpisy_PriestorId_KategoriaId_Rok",
                table: "Predpisy",
                columns: new[] { "PriestorId", "KategoriaId", "Rok" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriestorRoky_PriestorId_Rok",
                table: "PriestorRoky",
                columns: new[] { "PriestorId", "Rok" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Priestory_JednotkaId_Cislo",
                table: "Priestory",
                columns: new[] { "JednotkaId", "Cislo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Uhrady_Datum",
                table: "Uhrady",
                column: "Datum");

            migrationBuilder.CreateIndex(
                name: "IX_Uhrady_PriestorId",
                table: "Uhrady",
                column: "PriestorId");

            migrationBuilder.CreateIndex(
                name: "IX_Vlastnici_PriestorId",
                table: "Vlastnici",
                column: "PriestorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankoveUcty");

            migrationBuilder.DropTable(
                name: "NakladyRoka");

            migrationBuilder.DropTable(
                name: "Predpisy");

            migrationBuilder.DropTable(
                name: "PriestorRoky");

            migrationBuilder.DropTable(
                name: "Uhrady");

            migrationBuilder.DropTable(
                name: "Vlastnici");

            migrationBuilder.DropTable(
                name: "KategorieNakladov");

            migrationBuilder.DropTable(
                name: "Priestory");

            migrationBuilder.DropTable(
                name: "Jednotky");
        }
    }
}
