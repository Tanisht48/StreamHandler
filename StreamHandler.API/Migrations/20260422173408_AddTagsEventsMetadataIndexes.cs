using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StreamHandler.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTagsEventsMetadataIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BitrateBps",
                table: "Streams",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Codec",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "Streams",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Streams",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "StreamEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldStatus = table.Column<int>(type: "integer", nullable: false),
                    NewStatus = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamEvents_Streams_StreamId",
                        column: x => x.StreamId,
                        principalTable: "Streams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Streams_Status",
                table: "Streams",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Streams_Url",
                table: "Streams",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_OccurredAt",
                table: "StreamEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_StreamEvents_StreamId",
                table: "StreamEvents",
                column: "StreamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StreamEvents");

            migrationBuilder.DropIndex(
                name: "IX_Streams_Status",
                table: "Streams");

            migrationBuilder.DropIndex(
                name: "IX_Streams_Url",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "BitrateBps",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "Codec",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "Streams");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Streams");
        }
    }
}
