using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mastemis.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialProduction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AspNetRoles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: true),
                SecurityStamp = table.Column<string>(type: "text", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                PhoneNumber = table.Column<string>(type: "text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUsers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "audit_records",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                WorkerId = table.Column<Guid>(type: "uuid", nullable: true),
                Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ResourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                MetadataJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_records", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "candidates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_candidates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "exam_user_assignments",
            columns: table => new
            {
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_user_assignments", x => new { x.ExamId, x.UserId, x.Role });
            });

        migrationBuilder.CreateTable(
            name: "exams",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exams", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            columns: table => new
            {
                Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Caller = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_idempotency_records", x => new { x.Operation, x.Caller, x.Key });
            });

        migrationBuilder.CreateTable(
            name: "judge_workers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Capacity = table.Column<int>(type: "integer", nullable: false),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LastHeartbeatUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_judge_workers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                ContractVersion = table.Column<int>(type: "integer", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false),
                ResourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "room_invigilator_assignments",
            columns: table => new
            {
                RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_room_invigilator_assignments", x => new { x.RoomId, x.UserId });
            });

        migrationBuilder.CreateTable(
            name: "termination_metadata",
            columns: table => new
            {
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                WarningId = table.Column<Guid>(type: "uuid", nullable: false),
                FrozenRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                FinalSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                JudgeJobId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_termination_metadata", x => x.SessionId);
            });

        migrationBuilder.CreateTable(
            name: "AspNetRoleClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                ProviderKey = table.Column<string>(type: "text", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserRoles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "candidate_registrations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                RegistrationCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                AccessState = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_candidate_registrations", x => x.Id);
                table.ForeignKey(
                    name: "FK_candidate_registrations_candidates_CandidateId",
                    column: x => x.CandidateId,
                    principalTable: "candidates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_candidate_registrations_exams_ExamId",
                    column: x => x.ExamId,
                    principalTable: "exams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "exam_rooms",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_rooms", x => x.Id);
                table.ForeignKey(
                    name: "FK_exam_rooms_exams_ExamId",
                    column: x => x.ExamId,
                    principalTable: "exams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "worker_credentials",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WorkerId = table.Column<Guid>(type: "uuid", nullable: false),
                SecretHash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_worker_credentials", x => x.Id);
                table.ForeignKey(
                    name: "FK_worker_credentials_judge_workers_WorkerId",
                    column: x => x.WorkerId,
                    principalTable: "judge_workers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "exam_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                TerminatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CurrentRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                FrozenRevisionId = table.Column<Guid>(type: "uuid", nullable: true),
                Version = table.Column<int>(type: "integer", nullable: false),
                ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_exam_sessions_candidates_CandidateId",
                    column: x => x.CandidateId,
                    principalTable: "candidates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_exam_sessions_exam_rooms_RoomId",
                    column: x => x.RoomId,
                    principalTable: "exam_rooms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_exam_sessions_exams_ExamId",
                    column: x => x.ExamId,
                    principalTable: "exams",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "sfe_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientSequence = table.Column<long>(type: "bigint", nullable: false),
                ClientTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ServerReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                MetadataJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sfe_events", x => x.Id);
                table.ForeignKey(
                    name: "FK_sfe_events_exam_sessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "exam_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "source_revisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                ObjectId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Length = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_source_revisions", x => x.Id);
                table.ForeignKey(
                    name: "FK_source_revisions_exam_sessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "exam_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "sfe_evaluations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                Result = table.Column<int>(type: "integer", nullable: false),
                ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                PolicyVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                EvaluatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_sfe_evaluations", x => x.Id);
                table.ForeignKey(
                    name: "FK_sfe_evaluations_sfe_events_EventId",
                    column: x => x.EventId,
                    principalTable: "sfe_events",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "submissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                ProblemId = table.Column<Guid>(type: "uuid", nullable: false),
                RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                IsFinal = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_submissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_submissions_exam_sessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "exam_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_submissions_source_revisions_RevisionId",
                    column: x => x.RevisionId,
                    principalTable: "source_revisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "confirmed_warnings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                Ordinal = table.Column<int>(type: "integer", nullable: false),
                IssuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_confirmed_warnings", x => x.Id);
                table.ForeignKey(
                    name: "FK_confirmed_warnings_exam_sessions_SessionId",
                    column: x => x.SessionId,
                    principalTable: "exam_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_confirmed_warnings_sfe_evaluations_EvaluationId",
                    column: x => x.EvaluationId,
                    principalTable: "sfe_evaluations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "judge_jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                Priority = table.Column<int>(type: "integer", nullable: false),
                Attempt = table.Column<int>(type: "integer", nullable: false),
                MaximumAttempts = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                WorkerId = table.Column<Guid>(type: "uuid", nullable: true),
                LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_judge_jobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_judge_jobs_submissions_SubmissionId",
                    column: x => x.SubmissionId,
                    principalTable: "submissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "judgements",
            columns: table => new
            {
                SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                Verdict = table.Column<int>(type: "integer", nullable: false),
                Score = table.Column<int>(type: "integer", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_judgements", x => x.SubmissionId);
                table.ForeignKey(
                    name: "FK_judgements_submissions_SubmissionId",
                    column: x => x.SubmissionId,
                    principalTable: "submissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AspNetRoleClaims_RoleId",
            table: "AspNetRoleClaims",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "AspNetRoles",
            column: "NormalizedName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserClaims_UserId",
            table: "AspNetUserClaims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserLogins_UserId",
            table: "AspNetUserLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserRoles_RoleId",
            table: "AspNetUserRoles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "NormalizedEmail");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "NormalizedUserName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_audit_records_ActorUserId_OccurredAtUtc",
            table: "audit_records",
            columns: new[] { "ActorUserId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_audit_records_ResourceType_ResourceId_OccurredAtUtc",
            table: "audit_records",
            columns: new[] { "ResourceType", "ResourceId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_candidate_registrations_CandidateId",
            table: "candidate_registrations",
            column: "CandidateId");

        migrationBuilder.CreateIndex(
            name: "IX_candidate_registrations_ExamId_CandidateId",
            table: "candidate_registrations",
            columns: new[] { "ExamId", "CandidateId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_candidate_registrations_ExamId_RegistrationCode",
            table: "candidate_registrations",
            columns: new[] { "ExamId", "RegistrationCode" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_candidates_UserId",
            table: "candidates",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_confirmed_warnings_EvaluationId",
            table: "confirmed_warnings",
            column: "EvaluationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_confirmed_warnings_ExamId_RoomId_CandidateId_IssuedAtUtc",
            table: "confirmed_warnings",
            columns: new[] { "ExamId", "RoomId", "CandidateId", "IssuedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_confirmed_warnings_SessionId_Ordinal",
            table: "confirmed_warnings",
            columns: new[] { "SessionId", "Ordinal" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_exam_rooms_ExamId",
            table: "exam_rooms",
            column: "ExamId");

        migrationBuilder.CreateIndex(
            name: "IX_exam_rooms_ExamId_Code",
            table: "exam_rooms",
            columns: new[] { "ExamId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_exam_sessions_CandidateId_State",
            table: "exam_sessions",
            columns: new[] { "CandidateId", "State" });

        migrationBuilder.CreateIndex(
            name: "IX_exam_sessions_ExamId_CandidateId",
            table: "exam_sessions",
            columns: new[] { "ExamId", "CandidateId" },
            unique: true,
            filter: "\"State\" IN (1, 2)");

        migrationBuilder.CreateIndex(
            name: "IX_exam_sessions_ExamId_State",
            table: "exam_sessions",
            columns: new[] { "ExamId", "State" });

        migrationBuilder.CreateIndex(
            name: "IX_exam_sessions_RoomId_State",
            table: "exam_sessions",
            columns: new[] { "RoomId", "State" });

        migrationBuilder.CreateIndex(
            name: "IX_exam_user_assignments_UserId_ExamId",
            table: "exam_user_assignments",
            columns: new[] { "UserId", "ExamId" });

        migrationBuilder.CreateIndex(
            name: "IX_exams_State_StartsAtUtc",
            table: "exams",
            columns: new[] { "State", "StartsAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_judge_jobs_State_LeaseExpiresAtUtc",
            table: "judge_jobs",
            columns: new[] { "State", "LeaseExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_judge_jobs_State_Priority_AvailableAtUtc_CreatedAtUtc",
            table: "judge_jobs",
            columns: new[] { "State", "Priority", "AvailableAtUtc", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_judge_jobs_SubmissionId",
            table: "judge_jobs",
            column: "SubmissionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_judge_workers_LastHeartbeatUtc",
            table: "judge_workers",
            column: "LastHeartbeatUtc");

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_ProcessedAtUtc_NextAttemptAtUtc_CreatedAtUtc",
            table: "outbox_messages",
            columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_room_invigilator_assignments_UserId",
            table: "room_invigilator_assignments",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_sfe_evaluations_EventId",
            table: "sfe_evaluations",
            column: "EventId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_sfe_evaluations_SessionId_EvaluatedAtUtc",
            table: "sfe_evaluations",
            columns: new[] { "SessionId", "EvaluatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_sfe_events_SessionId_ClientSequence",
            table: "sfe_events",
            columns: new[] { "SessionId", "ClientSequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_sfe_events_SessionId_ServerReceivedAtUtc",
            table: "sfe_events",
            columns: new[] { "SessionId", "ServerReceivedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_source_revisions_SessionId_CreatedAtUtc",
            table: "source_revisions",
            columns: new[] { "SessionId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_submissions_RevisionId",
            table: "submissions",
            column: "RevisionId");

        migrationBuilder.CreateIndex(
            name: "IX_submissions_SessionId",
            table: "submissions",
            column: "SessionId",
            unique: true,
            filter: "\"IsFinal\" = TRUE");

        migrationBuilder.CreateIndex(
            name: "IX_submissions_SessionId_CreatedAtUtc",
            table: "submissions",
            columns: new[] { "SessionId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_termination_metadata_FinalSubmissionId",
            table: "termination_metadata",
            column: "FinalSubmissionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_termination_metadata_JudgeJobId",
            table: "termination_metadata",
            column: "JudgeJobId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_termination_metadata_WarningId",
            table: "termination_metadata",
            column: "WarningId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_worker_credentials_WorkerId_RevokedAtUtc",
            table: "worker_credentials",
            columns: new[] { "WorkerId", "RevokedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AspNetRoleClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        migrationBuilder.DropTable(
            name: "AspNetUserRoles");

        migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        migrationBuilder.DropTable(
            name: "audit_records");

        migrationBuilder.DropTable(
            name: "candidate_registrations");

        migrationBuilder.DropTable(
            name: "confirmed_warnings");

        migrationBuilder.DropTable(
            name: "exam_user_assignments");

        migrationBuilder.DropTable(
            name: "idempotency_records");

        migrationBuilder.DropTable(
            name: "judge_jobs");

        migrationBuilder.DropTable(
            name: "judgements");

        migrationBuilder.DropTable(
            name: "outbox_messages");

        migrationBuilder.DropTable(
            name: "room_invigilator_assignments");

        migrationBuilder.DropTable(
            name: "termination_metadata");

        migrationBuilder.DropTable(
            name: "worker_credentials");

        migrationBuilder.DropTable(
            name: "AspNetRoles");

        migrationBuilder.DropTable(
            name: "AspNetUsers");

        migrationBuilder.DropTable(
            name: "sfe_evaluations");

        migrationBuilder.DropTable(
            name: "submissions");

        migrationBuilder.DropTable(
            name: "judge_workers");

        migrationBuilder.DropTable(
            name: "sfe_events");

        migrationBuilder.DropTable(
            name: "source_revisions");

        migrationBuilder.DropTable(
            name: "exam_sessions");

        migrationBuilder.DropTable(
            name: "candidates");

        migrationBuilder.DropTable(
            name: "exam_rooms");

        migrationBuilder.DropTable(
            name: "exams");
    }
}
