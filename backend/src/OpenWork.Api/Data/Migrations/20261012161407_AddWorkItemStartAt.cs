using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemStartAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartAt",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartAt",
                table: "WorkItems");
        }
    }
}
