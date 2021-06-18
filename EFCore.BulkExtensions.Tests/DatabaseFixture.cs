using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DockerRunner;
using DockerRunner.Database.SqlServer;
using DockerRunner.Xunit;
using EFCore.BulkExtensions.SqlAdapters;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace EFCore.BulkExtensions.Tests
{
    [CollectionDefinition("Database")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
        // See https://xunit.net/docs/shared-context#collection-fixture
    }

    public class DatabaseFixture : IAsyncLifetime
    {
        private readonly DockerDatabaseContainerFixture<SqlServer2019Configuration> _sqlServerFixture;

        public DatabaseFixture(IMessageSink messageSink)
        {
            _sqlServerFixture = new DockerDatabaseContainerFixture<SqlServer2019Configuration>(messageSink);
        }

        public async Task InitializeAsync()
        {
            try
            {
                await ((IAsyncLifetime)_sqlServerFixture).InitializeAsync();
            }
            catch (DockerCommandException exception)
            {
                throw new Exception("Setting up the database fixture requires Docker to be installed and running.", exception);
            }
        }

        public async Task DisposeAsync()
        {
            await ((IAsyncLifetime)_sqlServerFixture).DisposeAsync();
        }

        public DbContextOptions GetOptions(DbServer dbServer, IInterceptor dbInterceptor) => GetOptions(dbServer, new[] { dbInterceptor });
        public DbContextOptions GetOptions(DbServer dbServer, IEnumerable<IInterceptor> dbInterceptors = null) => GetOptions<TestContext>(dbServer, dbInterceptors);

        public DbContextOptions GetOptions<TDbContext>(DbServer dbServer, IEnumerable<IInterceptor> dbInterceptors = null,
            string databaseName = nameof(EFCoreBulkTest))
            where TDbContext: DbContext
        {
            var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();

            if (dbServer == DbServer.SqlServer)
            {
                var connectionString = GetSqlServerConnectionString(databaseName);
                // ALTERNATIVELY (Using MSSQLLocalDB):
                //var connectionString = $@"Data Source=(localdb)\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=True";

                //optionsBuilder.UseSqlServer(connectionString); // Can NOT Test with UseInMemoryDb (Exception: Relational-specific methods can only be used when the context is using a relational)
                optionsBuilder.UseSqlServer(connectionString, opt => opt.UseNetTopologySuite()); // NetTopologySuite for Geometry / Geometry types
            }
            else if (dbServer == DbServer.Sqlite)
            {
                string connectionString = GetSqliteConnectionString(databaseName);
                optionsBuilder.UseSqlite(connectionString);
                SQLitePCL.Batteries.Init();

                // ALTERNATIVELY:
                //string connectionString = (new SqliteConnectionStringBuilder { DataSource = $"{databaseName}Lite.db" }).ToString();
                //optionsBuilder.UseSqlite(new SqliteConnection(connectionString));
            }
            else
            {
                throw new NotSupportedException($"Database {dbServer} is not supported. Only SQL Server and SQLite are Currently supported.");
            }

            if (dbInterceptors?.Any() == true)
            {
                optionsBuilder.AddInterceptors(dbInterceptors);
            }

            return optionsBuilder.Options;
        }
        
        public string GetSqlServerConnectionString(string databaseName)
        {
            return new SqlConnectionStringBuilder(_sqlServerFixture.ConnectionString) { InitialCatalog = databaseName }.ToString();
        }

        public string GetSqliteConnectionString(string databaseName)
        {
            return new SqliteConnectionStringBuilder { DataSource = $"{databaseName}.db" }.ToString();
        }
    }
    
    public class EnsureCreatedAndDeleted : IDisposable
    {
        private readonly DatabaseFacade _database;

        public EnsureCreatedAndDeleted(DatabaseFacade database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            database.EnsureCreated();
        }

        public void Dispose()
        {
            try
            {
                _database.EnsureDeleted();
            }
            catch (System.IO.IOException)
            {
                /*
                 * On Windows, we get an IOException, so instead of throwing (and thus failing the test),
                 * we let a dangling sqlite db file on disk after the tests execute.
                 * The process cannot access the file 'EFCore.BulkExtensions\EFCore.BulkExtensions.Tests\bin\Debug\net5.0\EFCoreBulkTest_ValueConverters.db' because it is being used by another process.
                 *   at System.IO.FileSystem.DeleteFile(String fullPath)
                 *   at System.IO.File.Delete(String path)
                 *   at Microsoft.EntityFrameworkCore.Sqlite.Storage.Internal.SqliteDatabaseCreator.Delete()
                 *   at Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator.EnsureDeleted()
                 *   at Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureDeleted()
                 *   at EFCore.BulkExtensions.Tests.EnsureCreatedAndDeleted.Dispose()
                 */
            }
        }
    }
}
