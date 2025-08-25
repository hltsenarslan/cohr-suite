using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Api.Migrations
{
    /// <inheritdoc />
    public partial class M3b_FixDeterministicSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW() AT TIME ZONE 'utc'",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("44709835-d55a-ef2a-2327-5fdca19e55d8"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 25, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW() AT TIME ZONE 'utc'");

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("44709835-d55a-ef2a-2327-5fdca19e55d8"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 25, 20, 43, 57, 58, DateTimeKind.Utc).AddTicks(9620));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac"),
                column: "CreatedAt",
                value: new DateTime(2025, 8, 25, 20, 43, 57, 58, DateTimeKind.Utc).AddTicks(9480));
        }
    }
}
