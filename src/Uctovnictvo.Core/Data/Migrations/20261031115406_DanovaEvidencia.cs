using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uctovnictvo.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class DanovaEvidencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KategoriaId",
                table: "PokladnicneDoklady",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KategoriaPohybuId",
                table: "Faktury",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KategoriaId",
                table: "BankovePohyby",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KategoriePohybov",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nazov = table.Column<string>(type: "TEXT", nullable: false),
                    Druh = table.Column<int>(type: "INTEGER", nullable: false),
                    Danovy = table.Column<bool>(type: "INTEGER", nullable: false),
                    Kod = table.Column<string>(type: "TEXT", nullable: true),
                    Poradie = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KategoriePohybov", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KategoriePohybov_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Majetok",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    InventarneCislo = table.Column<string>(type: "TEXT", nullable: false),
                    Nazov = table.Column<string>(type: "TEXT", nullable: false),
                    DatumObstarania = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DatumZaradenia = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    VstupnaCena = table.Column<decimal>(type: "TEXT", nullable: false),
                    OdpisovaSkupina = table.Column<int>(type: "INTEGER", nullable: false),
                    Metoda = table.Column<int>(type: "INTEGER", nullable: false),
                    PodielPodnikanie = table.Column<decimal>(type: "TEXT", nullable: false),
                    DatumVyradenia = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SposobVyradenia = table.Column<string>(type: "TEXT", nullable: true),
                    Poznamka = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Majetok", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Majetok_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vozidla",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nazov = table.Column<string>(type: "TEXT", nullable: false),
                    Ecv = table.Column<string>(type: "TEXT", nullable: true),
                    Spotreba = table.Column<decimal>(type: "TEXT", nullable: false),
                    Palivo = table.Column<string>(type: "TEXT", nullable: true),
                    VObchodnomMajetku = table.Column<bool>(type: "INTEGER", nullable: false),
                    CenaPaliva = table.Column<decimal>(type: "TEXT", nullable: false),
                    StavTachometraZaciatok = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vozidla", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vozidla_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Jazdy",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VozidloId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Odkial = table.Column<string>(type: "TEXT", nullable: false),
                    Kam = table.Column<string>(type: "TEXT", nullable: false),
                    Ucel = table.Column<string>(type: "TEXT", nullable: true),
                    Km = table.Column<decimal>(type: "TEXT", nullable: false),
                    Sukromna = table.Column<bool>(type: "INTEGER", nullable: false),
                    Vodic = table.Column<string>(type: "TEXT", nullable: true),
                    CenaPaliva = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jazdy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jazdy_Vozidla_VozidloId",
                        column: x => x.VozidloId,
                        principalTable: "Vozidla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PokladnicneDoklady_KategoriaId",
                table: "PokladnicneDoklady",
                column: "KategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Faktury_KategoriaPohybuId",
                table: "Faktury",
                column: "KategoriaPohybuId");

            migrationBuilder.CreateIndex(
                name: "IX_BankovePohyby_KategoriaId",
                table: "BankovePohyby",
                column: "KategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Jazdy_VozidloId_Datum",
                table: "Jazdy",
                columns: new[] { "VozidloId", "Datum" });

            migrationBuilder.CreateIndex(
                name: "IX_KategoriePohybov_JednotkaId",
                table: "KategoriePohybov",
                column: "JednotkaId");

            migrationBuilder.CreateIndex(
                name: "IX_Majetok_JednotkaId",
                table: "Majetok",
                column: "JednotkaId");

            migrationBuilder.CreateIndex(
                name: "IX_Vozidla_JednotkaId",
                table: "Vozidla",
                column: "JednotkaId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankovePohyby_KategoriePohybov_KategoriaId",
                table: "BankovePohyby",
                column: "KategoriaId",
                principalTable: "KategoriePohybov",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Faktury_KategoriePohybov_KategoriaPohybuId",
                table: "Faktury",
                column: "KategoriaPohybuId",
                principalTable: "KategoriePohybov",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PokladnicneDoklady_KategoriePohybov_KategoriaId",
                table: "PokladnicneDoklady",
                column: "KategoriaId",
                principalTable: "KategoriePohybov",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankovePohyby_KategoriePohybov_KategoriaId",
                table: "BankovePohyby");

            migrationBuilder.DropForeignKey(
                name: "FK_Faktury_KategoriePohybov_KategoriaPohybuId",
                table: "Faktury");

            migrationBuilder.DropForeignKey(
                name: "FK_PokladnicneDoklady_KategoriePohybov_KategoriaId",
                table: "PokladnicneDoklady");

            migrationBuilder.DropTable(
                name: "Jazdy");

            migrationBuilder.DropTable(
                name: "KategoriePohybov");

            migrationBuilder.DropTable(
                name: "Majetok");

            migrationBuilder.DropTable(
                name: "Vozidla");

            migrationBuilder.DropIndex(
                name: "IX_PokladnicneDoklady_KategoriaId",
                table: "PokladnicneDoklady");

            migrationBuilder.DropIndex(
                name: "IX_Faktury_KategoriaPohybuId",
                table: "Faktury");

            migrationBuilder.DropIndex(
                name: "IX_BankovePohyby_KategoriaId",
                table: "BankovePohyby");

            migrationBuilder.DropColumn(
                name: "KategoriaId",
                table: "PokladnicneDoklady");

            migrationBuilder.DropColumn(
                name: "KategoriaPohybuId",
                table: "Faktury");

            migrationBuilder.DropColumn(
                name: "KategoriaId",
                table: "BankovePohyby");
        }
    }
}
