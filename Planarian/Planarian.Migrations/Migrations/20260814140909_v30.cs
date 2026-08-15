using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v30 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MessageLogs historically persisted the complete substitutions JSON, including credential-bearing CTA URLs.
            // Preserve a useful safe target: strip query/fragment data and redact invitation codes embedded in the path.
            // Malformed historical JSON is skipped rather than blocking the schema upgrade.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    message_log RECORD;
                    substitutions_json jsonb;
                    button_url text;
                    sanitized_button_url text;
                BEGIN
                    FOR message_log IN
                        SELECT "Id", "Substitutions"
                        FROM "MessageLogs"
                        WHERE "Substitutions" LIKE '%"buttonUrl"%'
                    LOOP
                        BEGIN
                            substitutions_json := message_log."Substitutions"::jsonb;
                            button_url := substitutions_json ->> 'buttonUrl';

                            IF button_url IS NOT NULL THEN
                                sanitized_button_url := regexp_replace(button_url, '[?#].*$', '');
                                sanitized_button_url := regexp_replace(
                                    sanitized_button_url,
                                    '/user/invitations/[^/]+$',
                                    '/user/invitations/[redacted]',
                                    'i');

                                UPDATE "MessageLogs"
                                SET "Substitutions" = jsonb_set(
                                    substitutions_json,
                                    '{buttonUrl}',
                                    to_jsonb(sanitized_button_url),
                                    false)::text
                                WHERE "Id" = message_log."Id";
                            END IF;
                        EXCEPTION WHEN invalid_text_representation THEN
                            NULL;
                        END;
                    END LOOP;
                END $$;
                """);

            migrationBuilder.AddColumn<string>(
                name: "EmailConfirmationMessageLogId",
                table: "Users",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountInvitationAccountId",
                table: "MessageLogs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountInvitationUserId",
                table: "MessageLogs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "MessageLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatusMessage",
                table: "MessageLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryStatusOn",
                table: "MessageLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "MessageLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCorrelationId",
                table: "MessageLogs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderDomain",
                table: "MessageLogs",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                table: "MessageLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "MessageLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MessageLogEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MessageLogId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderEventType = table.Column<string>(type: "text", nullable: false),
                    ProviderEventId = table.Column<string>(type: "text", nullable: false),
                    ProviderEventDay = table.Column<DateOnly>(type: "date", nullable: false),
                    WebhookToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Severity = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    DeliveryCode = table.Column<string>(type: "text", nullable: true),
                    EnhancedDeliveryCode = table.Column<string>(type: "text", nullable: true),
                    DeliveryMessage = table.Column<string>(type: "text", nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: true),
                    IsDelayedBounce = table.Column<bool>(type: "boolean", nullable: true),
                    Bot = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageLogEvents_MessageLogs_MessageLogId",
                        column: x => x.MessageLogId,
                        principalTable: "MessageLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageLogEvents_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MessageLogEvents_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailConfirmationMessageLogId",
                table: "Users",
                column: "EmailConfirmationMessageLogId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_AccountInvitationAccountId_AccountInvitationUse~",
                table: "MessageLogs",
                columns: new[] { "AccountInvitationAccountId", "AccountInvitationUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_ProviderCorrelationId",
                table: "MessageLogs",
                column: "ProviderCorrelationId",
                unique: true,
                filter: "\"ProviderCorrelationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogEvents_CreatedByUserId",
                table: "MessageLogEvents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogEvents_MessageLogId",
                table: "MessageLogEvents",
                column: "MessageLogId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogEvents_ModifiedByUserId",
                table: "MessageLogEvents",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogEvents_Provider_ProviderDomain_ProviderEventDay_P~",
                table: "MessageLogEvents",
                columns: new[] { "Provider", "ProviderDomain", "ProviderEventDay", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogEvents_WebhookToken",
                table: "MessageLogEvents",
                column: "WebhookToken",
                unique: true,
                filter: "\"WebhookToken\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MessageLogs_AccountUsers_AccountInvitationAccountId_Account~",
                table: "MessageLogs",
                columns: new[] { "AccountInvitationAccountId", "AccountInvitationUserId" },
                principalTable: "AccountUsers",
                principalColumns: new[] { "AccountId", "UserId" },
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_MessageLogs_EmailConfirmationMessageLogId",
                table: "Users",
                column: "EmailConfirmationMessageLogId",
                principalTable: "MessageLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessageLogs_AccountUsers_AccountInvitationAccountId_Account~",
                table: "MessageLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_MessageLogs_EmailConfirmationMessageLogId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "MessageLogEvents");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmailConfirmationMessageLogId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_AccountInvitationAccountId_AccountInvitationUse~",
                table: "MessageLogs");

            migrationBuilder.DropIndex(
                name: "IX_MessageLogs_ProviderCorrelationId",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "EmailConfirmationMessageLogId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccountInvitationAccountId",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "AccountInvitationUserId",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "DeliveryStatusMessage",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "DeliveryStatusOn",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "ProviderCorrelationId",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "ProviderDomain",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "MessageLogs");

        }
    }
}
