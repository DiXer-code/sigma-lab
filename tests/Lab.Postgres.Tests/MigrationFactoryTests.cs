using Lab.Migrations.Postgres;
using Lab.Migrations.Sqlite;
using Lab.Postgres.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Lab.Postgres.Tests;

[Collection(PostgresTestGroup.Name)]
public sealed class MigrationFactoryTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void Factory_SelectsExpectedConnection_AndOwnMigrationSet(bool usePostgres, int source)
    {
        var specificKey = usePostgres
            ? PostgresDesignTimeDbContextFactory.ConnectionStringEnvironmentVariable
            : SqliteDesignTimeDbContextFactory.ConnectionStringEnvironmentVariable;
        var sharedKey = usePostgres ? "ConnectionStrings__Postgres" : "ConnectionStrings__Sqlite";
        var defaultConnection = usePostgres
            ? PostgresDesignTimeDbContextFactory.DefaultConnectionString
            : SqliteDesignTimeDbContextFactory.DefaultConnectionString;
        var sharedConnection = usePostgres ? "Host=shared;Database=lab;Username=lab" : "Data Source=shared.db";
        var specificConnection = usePostgres ? "Host=specific;Database=lab;Username=lab" : "Data Source=specific.db";
        var previousSpecific = Environment.GetEnvironmentVariable(specificKey);
        var previousShared = Environment.GetEnvironmentVariable(sharedKey);

        try
        {
            Environment.SetEnvironmentVariable(specificKey, source == 2 ? specificConnection : null);
            Environment.SetEnvironmentVariable(sharedKey, source >= 1 ? sharedConnection : null);
            using var context = usePostgres
                ? new PostgresDesignTimeDbContextFactory().CreateDbContext([])
                : new SqliteDesignTimeDbContextFactory().CreateDbContext([]);

            var expected = source switch { 2 => specificConnection, 1 => sharedConnection, _ => defaultConnection };
            Assert.Equal(expected, context.Database.GetConnectionString());
            Assert.Equal(usePostgres, context.Database.IsNpgsql());
            Assert.Equal(!usePostgres, context.Database.IsSqlite());
            var migrations = context.GetService<IMigrationsAssembly>();
            Assert.Equal(usePostgres ? "Lab.Migrations.Postgres" : "Lab.Migrations.Sqlite", migrations.Assembly.GetName().Name);
            Assert.NotEmpty(migrations.Migrations);
            Assert.NotNull(migrations.ModelSnapshot);
        }
        finally
        {
            Environment.SetEnvironmentVariable(specificKey, previousSpecific);
            Environment.SetEnvironmentVariable(sharedKey, previousShared);
        }
    }

    [Fact]
    public void Factories_RejectNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new SqliteDesignTimeDbContextFactory().CreateDbContext(null!));
        Assert.Throws<ArgumentNullException>(() => new PostgresDesignTimeDbContextFactory().CreateDbContext(null!));
    }
}
