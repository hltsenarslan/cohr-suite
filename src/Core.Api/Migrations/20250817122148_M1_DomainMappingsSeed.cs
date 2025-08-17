using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Core.Api.Migrations
{
    /// <inheritdoc />
    public partial class M1_DomainMappingsSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DomainMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Host = table.Column<string>(type: "text", nullable: false),
                    Module = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PathMode = table.Column<string>(type: "text", nullable: false),
                    TenantSlug = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomainMappings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "DomainMappings",
                columns: new[] { "Id", "Host", "IsActive", "Module", "PathMode", "TenantId", "TenantSlug" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "pys.local", true, "performance", "slug", null, null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "pay.local", true, "compensation", "slug", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DomainMappings_Host",
                table: "DomainMappings",
                column: "Host",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DomainMappings");
        }
    }
}
