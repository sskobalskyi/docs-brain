using DocsBrain.Api.Data;
using DocsBrain.Core.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DocsBrain.Tests;

/// <summary>One PostgreSQL with pgvector shared by all tests in the "postgres" collection.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:0.8.0-pg17")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection).ExecuteNonQueryAsync();
        }

        // Npgsql caches type info per pool; drop pools opened before the extension existed.
        NpgsqlConnection.ClearAllPools();
        await using var db = CreateDb(DocsBrainDbContext.AcmeTenantId);
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    public DocsBrainDbContext CreateDb(Guid tenant) =>
        new(new DbContextOptionsBuilder<DocsBrainDbContext>()
            .UseNpgsql(ConnectionString, o => o.UseVector()).Options, new FixedTenant(tenant));

    private sealed class FixedTenant(Guid id) : ITenantContext
    {
        public Guid? TenantId { get; } = id;
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
