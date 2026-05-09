using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSenderReceiverWallets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "wallets",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_wallets_TenantId_Currency",
                table: "wallets",
                newName: "IX_wallets_OwnerId_Currency");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "wallets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiverTenantId",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ReceiverWalletId",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SenderTenantId",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SenderWalletId",
                table: "payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "WalletId",
                table: "ledger_entries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_wallets_OwnerId",
                table: "wallets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_ReceiverTenantId",
                table: "payments",
                column: "ReceiverTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_ReceiverWalletId",
                table: "payments",
                column: "ReceiverWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_SenderTenantId",
                table: "payments",
                column: "SenderTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_SenderWalletId",
                table: "payments",
                column: "SenderWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_TenantId",
                table: "ledger_entries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_WalletId",
                table: "ledger_entries",
                column: "WalletId");

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_wallets_WalletId",
                table: "ledger_entries",
                column: "WalletId",
                principalTable: "wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_wallets_ReceiverWalletId",
                table: "payments",
                column: "ReceiverWalletId",
                principalTable: "wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_wallets_SenderWalletId",
                table: "payments",
                column: "SenderWalletId",
                principalTable: "wallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_wallets_WalletId",
                table: "ledger_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_wallets_ReceiverWalletId",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_wallets_SenderWalletId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_wallets_OwnerId",
                table: "wallets");

            migrationBuilder.DropIndex(
                name: "IX_payments_ReceiverTenantId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_ReceiverWalletId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_SenderTenantId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_SenderWalletId",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_TenantId",
                table: "ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_WalletId",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "wallets");

            migrationBuilder.DropColumn(
                name: "ReceiverTenantId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ReceiverWalletId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "SenderTenantId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "SenderWalletId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "WalletId",
                table: "ledger_entries");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "wallets",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_wallets_OwnerId_Currency",
                table: "wallets",
                newName: "IX_wallets_TenantId_Currency");
        }
    }
}
