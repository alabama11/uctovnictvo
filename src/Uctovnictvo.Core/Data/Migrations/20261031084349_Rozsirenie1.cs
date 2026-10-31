using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uctovnictvo.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Rozsirenie1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "VlastnikDo",
                table: "Vlastnici",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "VlastnikOd",
                table: "Vlastnici",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SpotrebaVymenenyVodomer",
                table: "PriestorRoky",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Nastavenia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kluc = table.Column<string>(type: "TEXT", nullable: false),
                    Hodnota = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nastavenia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PohybyFondu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Typ = table.Column<int>(type: "INTEGER", nullable: false),
                    Popis = table.Column<string>(type: "TEXT", nullable: false),
                    Suma = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PohybyFondu", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PohybyFondu_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Nastavenia_Kluc",
                table: "Nastavenia",
                column: "Kluc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PohybyFondu_JednotkaId",
                table: "PohybyFondu",
                column: "JednotkaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Nastavenia");

            migrationBuilder.DropTable(
                name: "PohybyFondu");

            migrationBuilder.DropColumn(
                name: "VlastnikDo",
                table: "Vlastnici");

            migrationBuilder.DropColumn(
                name: "VlastnikOd",
                table: "Vlastnici");

            migrationBuilder.DropColumn(
                name: "SpotrebaVymenenyVodomer",
                table: "PriestorRoky");
        }
    }
}
