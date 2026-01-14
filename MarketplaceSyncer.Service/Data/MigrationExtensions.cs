using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MarketplaceSyncer.Service.Data;

using Npgsql;

public static class MigrationExtensions
{
    public static IHost MigrateDatabase(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
        return host;
    }

    public static void EnsureDatabase(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var originalDatabase = builder.Database;
        
        // Switch to 'postgres' database to run administration commands
        builder.Database = "postgres";
        var masterConnectionString = builder.ToString();

        try 
        {
            using var connection = new NpgsqlConnection(masterConnectionString);
            connection.Open();

            var checkCommand = new NpgsqlCommand(
                $"SELECT 1 FROM pg_database WHERE datname = '{originalDatabase}'", connection);
        
            if (checkCommand.ExecuteScalar() == null)
            {
                var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{originalDatabase}\"", connection);
                createCommand.ExecuteNonQuery();
            }
        }
        catch (Exception)
        {
            // Fallback or ignore if user doesn't have permissions or connection fails
            // But usually we want to see this error if it happens.
            throw;
        }
    }
}
