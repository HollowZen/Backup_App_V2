using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptionPasswordHash",
                table: "BackupTasks",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAgeDays",
                table: "BackupTasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxVersions",
                table: "BackupTasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionPolicyType",
                table: "BackupTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "UseEncryption",
                table: "BackupTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptionPasswordHash",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "MaxAgeDays",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "MaxVersions",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyType",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "UseEncryption",
                table: "BackupTasks");
        }
    }
}
