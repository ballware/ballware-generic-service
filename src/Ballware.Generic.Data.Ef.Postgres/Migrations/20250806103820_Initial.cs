using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ballware.Generic.Data.Ef.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_connection",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    schema = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    connection_string = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    model = table.Column<string>(type: "text", nullable: true),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    create_stamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_changer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_change_stamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_connection", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_entity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: true),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    create_stamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_changer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_change_stamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_entity", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_connection_uuid",
                table: "tenant_connection",
                column: "uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_entity_tenant_id_entity",
                table: "tenant_entity",
                columns: new[] { "tenant_id", "entity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_entity_tenant_id_uuid",
                table: "tenant_entity",
                columns: new[] { "tenant_id", "uuid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_connection");

            migrationBuilder.DropTable(
                name: "tenant_entity");
        }
    }
}
