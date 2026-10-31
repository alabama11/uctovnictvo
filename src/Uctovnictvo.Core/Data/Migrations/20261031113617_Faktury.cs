using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uctovnictvo.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Faktury : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormatCislaFaktury",
                table: "Jednotky",
                type: "TEXT",
                nullable: false,
                defaultValue: "RRRR####");

            migrationBuilder.AddColumn<decimal>(
                name: "PokladnaPociatocnyStav",
                table: "Jednotky",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SplatnostDni",
                table: "Jednotky",
                type: "INTEGER",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.AddColumn<string>(
                name: "ZapisVRegistri",
                table: "Jednotky",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankovePohyby",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Suma = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProtiIban = table.Column<string>(type: "TEXT", nullable: true),
                    ProtiNazov = table.Column<string>(type: "TEXT", nullable: true),
                    VariabilnySymbol = table.Column<string>(type: "TEXT", nullable: true),
                    Popis = table.Column<string>(type: "TEXT", nullable: true),
                    Referencia = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankovePohyby", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankovePohyby_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Partneri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nazov = table.Column<string>(type: "TEXT", nullable: false),
                    Ico = table.Column<string>(type: "TEXT", nullable: true),
                    Dic = table.Column<string>(type: "TEXT", nullable: true),
                    IcDph = table.Column<string>(type: "TEXT", nullable: true),
                    Ulica = table.Column<string>(type: "TEXT", nullable: true),
                    Obec = table.Column<string>(type: "TEXT", nullable: true),
                    Psc = table.Column<string>(type: "TEXT", nullable: true),
                    Stat = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Telefon = table.Column<string>(type: "TEXT", nullable: true),
                    Iban = table.Column<string>(type: "TEXT", nullable: true),
                    Poznamka = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partneri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Partneri_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PokladnicneDoklady",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Druh = table.Column<int>(type: "INTEGER", nullable: false),
                    Cislo = table.Column<string>(type: "TEXT", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Suma = table.Column<decimal>(type: "TEXT", nullable: false),
                    Ucel = table.Column<string>(type: "TEXT", nullable: false),
                    Osoba = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokladnicneDoklady", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PokladnicneDoklady_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Faktury",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Druh = table.Column<int>(type: "INTEGER", nullable: false),
                    Typ = table.Column<int>(type: "INTEGER", nullable: false),
                    Cislo = table.Column<string>(type: "TEXT", nullable: false),
                    CisloDodavatela = table.Column<string>(type: "TEXT", nullable: true),
                    VariabilnySymbol = table.Column<string>(type: "TEXT", nullable: true),
                    KonstantnySymbol = table.Column<string>(type: "TEXT", nullable: true),
                    PartnerId = table.Column<int>(type: "INTEGER", nullable: true),
                    PartnerNazov = table.Column<string>(type: "TEXT", nullable: false),
                    PartnerAdresa = table.Column<string>(type: "TEXT", nullable: true),
                    PartnerIco = table.Column<string>(type: "TEXT", nullable: true),
                    PartnerDic = table.Column<string>(type: "TEXT", nullable: true),
                    PartnerIcDph = table.Column<string>(type: "TEXT", nullable: true),
                    PartnerIban = table.Column<string>(type: "TEXT", nullable: true),
                    DatumVystavenia = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DatumDodania = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DatumSplatnosti = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    FormaUhrady = table.Column<int>(type: "INTEGER", nullable: false),
                    PovodnaFaktura = table.Column<string>(type: "TEXT", nullable: true),
                    TextNad = table.Column<string>(type: "TEXT", nullable: true),
                    TextPod = table.Column<string>(type: "TEXT", nullable: true),
                    KategoriaNakladovId = table.Column<int>(type: "INTEGER", nullable: true),
                    DatumUpomienky = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faktury", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Faktury_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Faktury_KategorieNakladov_KategoriaNakladovId",
                        column: x => x.KategoriaNakladovId,
                        principalTable: "KategorieNakladov",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Faktury_Partneri_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partneri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PolozkyFaktur",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FakturaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Poradie = table.Column<int>(type: "INTEGER", nullable: false),
                    Popis = table.Column<string>(type: "TEXT", nullable: false),
                    Mnozstvo = table.Column<decimal>(type: "TEXT", nullable: false),
                    Jednotka = table.Column<string>(type: "TEXT", nullable: true),
                    CenaZaJednotku = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolozkyFaktur", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolozkyFaktur_Faktury_FakturaId",
                        column: x => x.FakturaId,
                        principalTable: "Faktury",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UhradyFaktur",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FakturaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Suma = table.Column<decimal>(type: "TEXT", nullable: false),
                    BankovyPohybId = table.Column<int>(type: "INTEGER", nullable: true),
                    PokladnicnyDokladId = table.Column<int>(type: "INTEGER", nullable: true),
                    Poznamka = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UhradyFaktur", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UhradyFaktur_BankovePohyby_BankovyPohybId",
                        column: x => x.BankovyPohybId,
                        principalTable: "BankovePohyby",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UhradyFaktur_Faktury_FakturaId",
                        column: x => x.FakturaId,
                        principalTable: "Faktury",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UhradyFaktur_PokladnicneDoklady_PokladnicnyDokladId",
                        column: x => x.PokladnicnyDokladId,
                        principalTable: "PokladnicneDoklady",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankovePohyby_JednotkaId_Datum",
                table: "BankovePohyby",
                columns: new[] { "JednotkaId", "Datum" });

            migrationBuilder.CreateIndex(
                name: "IX_BankovePohyby_JednotkaId_Referencia",
                table: "BankovePohyby",
                columns: new[] { "JednotkaId", "Referencia" });

            migrationBuilder.CreateIndex(
                name: "IX_Faktury_JednotkaId_Druh_Cislo",
                table: "Faktury",
                columns: new[] { "JednotkaId", "Druh", "Cislo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Faktury_KategoriaNakladovId",
                table: "Faktury",
                column: "KategoriaNakladovId");

            migrationBuilder.CreateIndex(
                name: "IX_Faktury_PartnerId",
                table: "Faktury",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Partneri_JednotkaId_Ico",
                table: "Partneri",
                columns: new[] { "JednotkaId", "Ico" });

            migrationBuilder.CreateIndex(
                name: "IX_PokladnicneDoklady_JednotkaId_Cislo",
                table: "PokladnicneDoklady",
                columns: new[] { "JednotkaId", "Cislo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolozkyFaktur_FakturaId",
                table: "PolozkyFaktur",
                column: "FakturaId");

            migrationBuilder.CreateIndex(
                name: "IX_UhradyFaktur_BankovyPohybId",
                table: "UhradyFaktur",
                column: "BankovyPohybId");

            migrationBuilder.CreateIndex(
                name: "IX_UhradyFaktur_FakturaId",
                table: "UhradyFaktur",
                column: "FakturaId");

            migrationBuilder.CreateIndex(
                name: "IX_UhradyFaktur_PokladnicnyDokladId",
                table: "UhradyFaktur",
                column: "PokladnicnyDokladId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PolozkyFaktur");

            migrationBuilder.DropTable(
                name: "UhradyFaktur");

            migrationBuilder.DropTable(
                name: "BankovePohyby");

            migrationBuilder.DropTable(
                name: "Faktury");

            migrationBuilder.DropTable(
                name: "PokladnicneDoklady");

            migrationBuilder.DropTable(
                name: "Partneri");

            migrationBuilder.DropColumn(
                name: "FormatCislaFaktury",
                table: "Jednotky");

            migrationBuilder.DropColumn(
                name: "PokladnaPociatocnyStav",
                table: "Jednotky");

            migrationBuilder.DropColumn(
                name: "SplatnostDni",
                table: "Jednotky");

            migrationBuilder.DropColumn(
                name: "ZapisVRegistri",
                table: "Jednotky");
        }
    }
}
