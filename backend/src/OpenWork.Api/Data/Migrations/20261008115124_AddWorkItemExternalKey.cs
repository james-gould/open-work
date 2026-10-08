using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemExternalKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalKey",
                table: "WorkItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ExternalKey",
                table: "WorkItems",
                column: "ExternalKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ExternalKey",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ExternalKey",
                table: "WorkItems");
        }
    }
}
