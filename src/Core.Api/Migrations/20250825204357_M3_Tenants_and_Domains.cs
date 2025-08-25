using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Core.Api.Migrations
{
    /// <inheritdoc />
    public partial class M3_Tenants_and_Domains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Host = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantDomains_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "CreatedAt", "Name", "Slug", "Status" },
                values: new object[,]
                {
                    { new Guid("44709835-d55a-ef2a-2327-5fdca19e55d8"), new DateTime(2025, 8, 25, 20, 43, 57, 58, DateTimeKind.Utc).AddTicks(9620), "Firm 2", "firm2", "active" },
                    { new Guid("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac"), new DateTime(2025, 8, 25, 20, 43, 57, 58, DateTimeKind.Utc).AddTicks(9480), "Firm 1", "firm1", "active" }
                });

            migrationBuilder.InsertData(
                table: "TenantDomains",
                columns: new[] { "Id", "Host", "IsDefault", "TenantId" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333331"), "pys.local", true, new Guid("a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac") },
                    { new Guid("33333333-3333-3333-3333-333333333332"), "pay.local", true, new Guid("44709835-d55a-ef2a-2327-5fdca19e55d8") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantDomains_Host",
                table: "TenantDomains",
                column: "Host",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantDomains_TenantId",
                table: "TenantDomains",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantDomains");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
