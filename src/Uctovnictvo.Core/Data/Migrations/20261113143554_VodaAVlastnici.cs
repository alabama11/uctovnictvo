using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uctovnictvo.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class VodaAVlastnici : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "JeVlastnik",
                table: "Vlastnici",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SpoluvlastnickyPodiel",
                table: "Vlastnici",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CisloVodomeru2",
                table: "Priestory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StavVodomeru2Koniec",
                table: "PriestorRoky",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StavVodomeru2Zaciatok",
                table: "PriestorRoky",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VodaRoky",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JednotkaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Rok = table.Column<int>(type: "INTEGER", nullable: false),
                    Datum1 = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Datum2 = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Datum3 = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Datum4 = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Cena1 = table.Column<decimal>(type: "TEXT", nullable: false),
                    Cena2 = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VodaRoky", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VodaRoky_Jednotky_JednotkaId",
                        column: x => x.JednotkaId,
                        principalTable: "Jednotky",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VodaRoky_JednotkaId_Rok",
                table: "VodaRoky",
                columns: new[] { "JednotkaId", "Rok" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VodaRoky");

            migrationBuilder.DropColumn(
                name: "JeVlastnik",
                table: "Vlastnici");

            migrationBuilder.DropColumn(
                name: "SpoluvlastnickyPodiel",
                table: "Vlastnici");

            migrationBuilder.DropColumn(
                name: "CisloVodomeru2",
                table: "Priestory");

            migrationBuilder.DropColumn(
                name: "StavVodomeru2Koniec",
                table: "PriestorRoky");

            migrationBuilder.DropColumn(
                name: "StavVodomeru2Zaciatok",
                table: "PriestorRoky");
        }
    }
}
