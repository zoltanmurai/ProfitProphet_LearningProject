using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProfitProphet.Migrations
{
    /// <inheritdoc />
    public partial class DropTickerFkFromCandles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) FK dobása (ha még létezik)
            migrationBuilder.DropForeignKey(
                name: "FK_Candles_Tickers_TickerId",
                table: "Candles");

            // 2) (ha volt index) dobd az indexet is
            migrationBuilder.DropIndex(
                name: "IX_Candles_TickerId",
                table: "Candles");

            // 3) Timeframe stringgé (ha még int volt)
            migrationBuilder.AlterColumn<string>(
                name: "Timeframe",
                table: "Candles",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            // 4) TickerId oszlop ELTÁVOLÍTÁSA
            migrationBuilder.DropColumn(
                name: "TickerId",
                table: "Candles");
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1) TickerId vissza
            migrationBuilder.AddColumn<int>(
                name: "TickerId",
                table: "Candles",
                type: "INTEGER",
                nullable: true);

            // 2) Timeframe vissza intre
            migrationBuilder.AlterColumn<int>(
                name: "Timeframe",
                table: "Candles",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            // 3) Index + FK vissza
            migrationBuilder.CreateIndex(
                name: "IX_Candles_TickerId",
                table: "Candles",
                column: "TickerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Candles_Tickers_TickerId",
                table: "Candles",
                column: "TickerId",
                principalTable: "Tickers",
                principalColumn: "Id");
        }
    }
}
