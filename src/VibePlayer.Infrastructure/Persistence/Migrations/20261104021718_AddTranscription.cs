using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibePlayer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTranscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transcripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: true),
                    Language = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transcripts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LessonTranscriptions",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TranscriptId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VideoHash = table.Column<string>(type: "TEXT", nullable: true),
                    HashedFileSize = table.Column<long>(type: "INTEGER", nullable: true),
                    HashedLastWriteUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", nullable: true),
                    QueuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    PreferWhisper = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonTranscriptions", x => x.LessonId);
                    table.ForeignKey(
                        name: "FK_LessonTranscriptions_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LessonTranscriptions_Transcripts_TranscriptId",
                        column: x => x.TranscriptId,
                        principalTable: "Transcripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TranscriptSegments",
                columns: table => new
                {
                    TranscriptId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    StartMs = table.Column<long>(type: "INTEGER", nullable: false),
                    EndMs = table.Column<long>(type: "INTEGER", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranscriptSegments", x => new { x.TranscriptId, x.Position });
                    table.ForeignKey(
                        name: "FK_TranscriptSegments_Transcripts_TranscriptId",
                        column: x => x.TranscriptId,
                        principalTable: "Transcripts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonTranscriptions_Status",
                table: "LessonTranscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LessonTranscriptions_TranscriptId",
                table: "LessonTranscriptions",
                column: "TranscriptId");

            migrationBuilder.CreateIndex(
                name: "IX_Transcripts_SourceHash_Source",
                table: "Transcripts",
                columns: new[] { "SourceHash", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonTranscriptions");

            migrationBuilder.DropTable(
                name: "TranscriptSegments");

            migrationBuilder.DropTable(
                name: "Transcripts");
        }
    }
}
