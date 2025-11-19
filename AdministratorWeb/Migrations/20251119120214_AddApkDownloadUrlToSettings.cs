using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdministratorWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddApkDownloadUrlToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewEmail",
                table: "OTPCodes",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "OTPCodes",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ApkDownloadUrl",
                table: "LaundrySettings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewEmail",
                table: "OTPCodes");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "OTPCodes");

            migrationBuilder.DropColumn(
                name: "ApkDownloadUrl",
                table: "LaundrySettings");
        }
    }
}
