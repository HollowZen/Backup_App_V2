using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BackupType",
                table: "BackupTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompressionLevel",
                table: "BackupTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastBackupTime",
                table: "BackupTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFullBackupTime",
                table: "BackupTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseCompression",
                table: "BackupTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BackupType",
                table: "BackupHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CompressionLevel",
                table: "BackupHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "CompressedSize",
                table: "BackupHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "BackupHistories",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0));

            migrationBuilder.AddColumn<string>(
                name: "OutputArtifactPath",
                table: "BackupHistories",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsedCompression",
                table: "BackupHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackupType",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "CompressionLevel",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "LastBackupTime",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "LastFullBackupTime",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "UseCompression",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "BackupType",
                table: "BackupHistories");

            migrationBuilder.DropColumn(
                name: "CompressionLevel",
                table: "BackupHistories");

            migrationBuilder.DropColumn(
                name: "CompressedSize",
                table: "BackupHistories");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "BackupHistories");

            migrationBuilder.DropColumn(
                name: "OutputArtifactPath",
                table: "BackupHistories");

            migrationBuilder.DropColumn(
                name: "UsedCompression",
                table: "BackupHistories");
        }
    }
}






