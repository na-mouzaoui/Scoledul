using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timetable.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generation_preference",
                columns: table => new
                {
                    code = table.Column<string>(type: "TEXT", nullable: false),
                    weight = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_preference", x => x.code);
                    table.CheckConstraint("ck_preference_weight", "weight BETWEEN 0 AND 10");
                });

            migrationBuilder.CreateTable(
                name: "project_setting",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_setting", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "school_class",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_class", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "school_day",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    weekday = table.Column<int>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    start_minute = table.Column<int>(type: "INTEGER", nullable: false),
                    end_minute = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_day", x => x.id);
                    table.CheckConstraint("ck_school_day_active", "is_active IN (0, 1)");
                    table.CheckConstraint("ck_school_day_minutes", "start_minute BETWEEN 0 AND 1439 AND end_minute BETWEEN 1 AND 1440 AND end_minute > start_minute");
                    table.CheckConstraint("ck_school_day_weekday", "weekday BETWEEN 1 AND 7");
                });

            migrationBuilder.CreateTable(
                name: "subject",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    color_hex = table.Column<string>(type: "TEXT", nullable: false),
                    block_size = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subject", x => x.id);
                    table.CheckConstraint("ck_subject_block_size", "block_size BETWEEN 1 AND 4");
                });

            migrationBuilder.CreateTable(
                name: "timetable_version",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<string>(type: "TEXT", nullable: false),
                    is_selected = table.Column<bool>(type: "INTEGER", nullable: false),
                    solver_seed = table.Column<int>(type: "INTEGER", nullable: true),
                    score = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_timetable_version", x => x.id);
                    table.CheckConstraint("ck_version_selected", "is_selected IN (0, 1)");
                });

            migrationBuilder.CreateTable(
                name: "break_period",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    day_id = table.Column<int>(type: "INTEGER", nullable: false),
                    label = table.Column<string>(type: "TEXT", nullable: false),
                    start_minute = table.Column<int>(type: "INTEGER", nullable: false),
                    end_minute = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_break_period", x => x.id);
                    table.CheckConstraint("ck_break_period_minutes", "end_minute > start_minute");
                    table.ForeignKey(
                        name: "FK_break_period_school_day_day_id",
                        column: x => x.day_id,
                        principalTable: "school_day",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "time_slot",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    day_id = table.Column<int>(type: "INTEGER", nullable: false),
                    slot_index = table.Column<int>(type: "INTEGER", nullable: false),
                    start_minute = table.Column<int>(type: "INTEGER", nullable: false),
                    end_minute = table.Column<int>(type: "INTEGER", nullable: false),
                    follows_break = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_slot", x => x.id);
                    table.CheckConstraint("ck_time_slot_follows_break", "follows_break IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_time_slot_school_day_day_id",
                        column: x => x.day_id,
                        principalTable: "school_day",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    last_name = table.Column<string>(type: "TEXT", nullable: false),
                    first_name = table.Column<string>(type: "TEXT", nullable: false),
                    subject_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher", x => x.id);
                    table.ForeignKey(
                        name: "FK_teacher_subject_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subject",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_subject_requirement",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    class_id = table.Column<int>(type: "INTEGER", nullable: false),
                    subject_id = table.Column<int>(type: "INTEGER", nullable: false),
                    weekly_sessions = table.Column<int>(type: "INTEGER", nullable: false),
                    fixed_teacher_id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_class_subject_requirement", x => x.id);
                    table.CheckConstraint("ck_requirement_weekly", "weekly_sessions >= 0");
                    table.ForeignKey(
                        name: "FK_class_subject_requirement_school_class_class_id",
                        column: x => x.class_id,
                        principalTable: "school_class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_class_subject_requirement_subject_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subject",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_class_subject_requirement_teacher_fixed_teacher_id",
                        column: x => x.fixed_teacher_id,
                        principalTable: "teacher",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "lesson",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    version_id = table.Column<int>(type: "INTEGER", nullable: false),
                    class_id = table.Column<int>(type: "INTEGER", nullable: false),
                    subject_id = table.Column<int>(type: "INTEGER", nullable: false),
                    teacher_id = table.Column<int>(type: "INTEGER", nullable: false),
                    time_slot_id = table.Column<int>(type: "INTEGER", nullable: false),
                    block_key = table.Column<string>(type: "TEXT", nullable: true),
                    is_locked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson", x => x.id);
                    table.CheckConstraint("ck_lesson_locked", "is_locked IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_lesson_school_class_class_id",
                        column: x => x.class_id,
                        principalTable: "school_class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lesson_subject_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subject",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_teacher_teacher_id",
                        column: x => x.teacher_id,
                        principalTable: "teacher",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lesson_time_slot_time_slot_id",
                        column: x => x.time_slot_id,
                        principalTable: "time_slot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_lesson_timetable_version_version_id",
                        column: x => x.version_id,
                        principalTable: "timetable_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher_unavailability",
                columns: table => new
                {
                    teacher_id = table.Column<int>(type: "INTEGER", nullable: false),
                    time_slot_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_unavailability", x => new { x.teacher_id, x.time_slot_id });
                    table.ForeignKey(
                        name: "FK_teacher_unavailability_teacher_teacher_id",
                        column: x => x.teacher_id,
                        principalTable: "teacher",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_teacher_unavailability_time_slot_time_slot_id",
                        column: x => x.time_slot_id,
                        principalTable: "time_slot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_break_period_day_id",
                table: "break_period",
                column: "day_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_subject_requirement_class_id_subject_id",
                table: "class_subject_requirement",
                columns: new[] { "class_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_class_subject_requirement_fixed_teacher_id",
                table: "class_subject_requirement",
                column: "fixed_teacher_id");

            migrationBuilder.CreateIndex(
                name: "IX_class_subject_requirement_subject_id",
                table: "class_subject_requirement",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_class_id",
                table: "lesson",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_subject_id",
                table: "lesson",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_teacher_id",
                table: "lesson",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "IX_lesson_time_slot_id",
                table: "lesson",
                column: "time_slot_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_version_class",
                table: "lesson",
                columns: new[] { "version_id", "class_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lesson_version_teacher",
                table: "lesson",
                columns: new[] { "version_id", "teacher_id" });

            migrationBuilder.CreateIndex(
                name: "ux_lesson_version_class_slot",
                table: "lesson",
                columns: new[] { "version_id", "class_id", "time_slot_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_lesson_version_teacher_slot",
                table: "lesson",
                columns: new[] { "version_id", "teacher_id", "time_slot_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_school_class_name",
                table: "school_class",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_school_day_weekday",
                table: "school_day",
                column: "weekday",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subject_name",
                table: "subject",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_last_name_first_name_subject_id",
                table: "teacher",
                columns: new[] { "last_name", "first_name", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teacher_subject",
                table: "teacher",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_teacher_unavailability_time_slot_id",
                table: "teacher_unavailability",
                column: "time_slot_id");

            migrationBuilder.CreateIndex(
                name: "IX_time_slot_day_id_slot_index",
                table: "time_slot",
                columns: new[] { "day_id", "slot_index" },
                unique: true);

            // HC-05 : l'enseignant affecté doit enseigner la matière de la séance.
            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_lesson_teacher_subject
                BEFORE INSERT ON lesson
                BEGIN
                    SELECT RAISE(ABORT, 'Enseignant incompatible avec la matière')
                    WHERE (SELECT subject_id FROM teacher WHERE id = NEW.teacher_id) <> NEW.subject_id;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_lesson_teacher_subject_update
                BEFORE UPDATE OF teacher_id, subject_id ON lesson
                BEGIN
                    SELECT RAISE(ABORT, 'Enseignant incompatible avec la matière')
                    WHERE (SELECT subject_id FROM teacher WHERE id = NEW.teacher_id) <> NEW.subject_id;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_lesson_teacher_subject;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_lesson_teacher_subject_update;");

            migrationBuilder.DropTable(
                name: "break_period");

            migrationBuilder.DropTable(
                name: "class_subject_requirement");

            migrationBuilder.DropTable(
                name: "generation_preference");

            migrationBuilder.DropTable(
                name: "lesson");

            migrationBuilder.DropTable(
                name: "project_setting");

            migrationBuilder.DropTable(
                name: "teacher_unavailability");

            migrationBuilder.DropTable(
                name: "school_class");

            migrationBuilder.DropTable(
                name: "timetable_version");

            migrationBuilder.DropTable(
                name: "teacher");

            migrationBuilder.DropTable(
                name: "time_slot");

            migrationBuilder.DropTable(
                name: "subject");

            migrationBuilder.DropTable(
                name: "school_day");
        }
    }
}
