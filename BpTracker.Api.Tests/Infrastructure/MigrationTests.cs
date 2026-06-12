using System;
using System.Data.Common;
using System.Threading.Tasks;
using BpTracker.Api.Data;
using BpTracker.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BpTracker.Api.Tests.Infrastructure;

public class MigrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MigrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ModelDateTimeOffset_MapsToTimestamptzColumns()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();

        var targetColumns = new[]
        {
            ("Users", "CreatedAt"),
            ("Users", "LastExportAt"),
            ("UserSessions", "CreatedAt"),
            ("UserSessions", "ExpiresAt"),
            ("UserCredentials", "CreatedAt"),
            ("UserCredentials", "LastUsedAt"),
            ("MagicLinks", "CreatedAt"),
            ("MagicLinks", "ExpiresAt"),
            ("EmailOutbox", "CreatedAt"),
            ("EmailOutbox", "NextAttemptAt"),
            ("Measurements", "RecordedAt"),
            ("PushSubscriptions", "CreatedAt")
        };

        foreach (var (table, column) in targetColumns)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                SELECT data_type 
                FROM information_schema.columns 
                WHERE table_schema = 'public' 
                  AND table_name = '{table}' 
                  AND column_name = '{column}';";
            
            var dataType = await cmd.ExecuteScalarAsync() as string;
            dataType.Should().Be("timestamp with time zone", $"column {table}.{column} should be mapped to timestamptz in PostgreSQL");
        }
    }
}
