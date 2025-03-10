using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpleMailArchiver.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if the table exists and only create it if it doesn't exist
            migrationBuilder.Sql(@"
        CREATE TABLE IF NOT EXISTS MailMessages (
            ID INTEGER PRIMARY KEY AUTOINCREMENT,
            HASH TEXT,
            SUBJECT TEXT,
            SENDER TEXT,
            RECIPIENT TEXT,
            CC_RECIPIENT TEXT,
            BCC_RECIPIENT TEXT,
            RECEIVE_TIME TEXT NOT NULL,
            ATTACHMENTS TEXT,
            FOLDER TEXT,
            MESSAGE TEXT,
            HtmlBody TEXT
        );
    ");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailMessages");
        }
    }
}
