using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OpenWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthAndWorkItemExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuthConfig",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PinHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    PinSalt = table.Column<byte[]>(type: "bytea", nullable: false),
                    PinIterations = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecoveryCredentialId = table.Column<byte[]>(type: "bytea", nullable: true),
                    RecoveryPublicKey = table.Column<byte[]>(type: "bytea", nullable: true),
                    RecoverySignCount = table.Column<long>(type: "bigint", nullable: false),
                    RecoveryUserHandle = table.Column<byte[]>(type: "bytea", nullable: true),
                    RecoveryBackupEligible = table.Column<bool>(type: "boolean", nullable: false),
                    RecoveryRegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PendingCeremonyOptionsJson = table.Column<string>(type: "text", nullable: true),
                    PendingCeremonyExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthConfig", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthConfig");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "WorkItems");
        }
    }
}
