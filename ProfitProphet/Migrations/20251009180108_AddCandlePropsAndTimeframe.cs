using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProfitProphet.Migrations
{
    /// <inheritdoc />
    public partial class AddCandlePropsAndTimeframe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Date",
                table: "Candles",
                newName: "TimestampUtc");

            migrationBuilder.AlterColumn<string>(
                name: "Symbol",
                table: "Tickers",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Tickers",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "Volume",
                table: "Candles",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "Candles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Timeframe",
                table: "Candles",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Candles_Symbol_TimestampUtc_Timeframe",
                table: "Candles",
                columns: new[] { "Symbol", "TimestampUtc", "Timeframe" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Candles_Symbol_TimestampUtc_Timeframe",
                table: "Candles");

            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "Candles");

            migrationBuilder.DropColumn(
                name: "Timeframe",
                table: "Candles");

            migrationBuilder.RenameColumn(
                name: "TimestampUtc",
                table: "Candles",
                newName: "Date");

            migrationBuilder.AlterColumn<string>(
                name: "Symbol",
                table: "Tickers",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Tickers",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "Volume",
                table: "Candles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
