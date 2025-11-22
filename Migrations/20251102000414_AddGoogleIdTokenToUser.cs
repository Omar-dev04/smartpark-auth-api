using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth_API.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleIdTokenToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleIdToken",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleIdToken",
                table: "Users");
        }
    }
}
