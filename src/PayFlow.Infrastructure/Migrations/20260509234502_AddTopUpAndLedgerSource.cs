using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTopUpAndLedgerSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ledger_entries",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Payment");

            migrationBuilder.CreateTable(
                name: "top_ups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_top_ups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_top_ups_wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_top_ups_TenantId",
                table: "top_ups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_top_ups_WalletId",
                table: "top_ups",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_top_ups_WalletId_IdempotencyKey",
                table: "top_ups",
                columns: new[] { "WalletId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "top_ups");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ledger_entries");
        }
    }
}
