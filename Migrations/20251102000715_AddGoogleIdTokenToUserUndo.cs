using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth_API.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleIdTokenToUserUndo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleIdToken",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleIdToken",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
