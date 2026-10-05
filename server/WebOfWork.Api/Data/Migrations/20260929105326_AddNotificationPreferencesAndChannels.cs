using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferencesAndChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_registrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    device_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_registrations", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_registrations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    in_app_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    push_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sms_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    master_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_type_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_type_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_type_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_registrations_token",
                table: "device_registrations",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_registrations_user_id",
                table: "device_registrations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_preferences_user_id",
                table: "notification_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_type_preferences_user_id_type_channel",
                table: "notification_type_preferences",
                columns: new[] { "user_id", "type", "channel" },
                unique: true);

            // EF scaffolded `sms_enabled` with `defaultValue: true`, copying the
            // value the property had when the table was first scaffolded. The
            // property is now `= false` (SMS costs money per message and nobody
            // should be opted into it by accident), but a column default only
            // applies to rows inserted without a value, and a database that
            // already has `notification_preferences` rows would keep them at true.
            //
            // This migration creates the table, so there is nothing to back-fill
            // yet -- but the default is the part that will bite the next time the
            // table is populated by anything other than this migration, so it is
            // corrected here rather than left to surprise someone.
            migrationBuilder.Sql("""
                ALTER TABLE notification_preferences
                ALTER COLUMN sms_enabled SET DEFAULT FALSE;
                """);

            // `email_enabled` and `master_enabled` were scaffolded with no column
            // default, because their CLR defaults are applied in the entity rather
            // than the schema. That is fine -- EF always writes every column -- but
            // an explicit default keeps a hand-inserted row from landing as
            // `false` and silently appearing to have notifications switched off.
            migrationBuilder.Sql("""
                ALTER TABLE notification_preferences
                ALTER COLUMN email_enabled SET DEFAULT TRUE;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE notification_preferences
                ALTER COLUMN master_enabled SET DEFAULT TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_registrations");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notification_type_preferences");
        }
    }
}
