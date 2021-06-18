using EFCore.BulkExtensions.SqlAdapters;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EFCore.BulkExtensions.Tests.ValueConverters
{
    [Collection("Database")]
    public class ValueConverterTests
    {
        private readonly DatabaseFixture _databaseFixture;

        public ValueConverterTests(DatabaseFixture databaseFixture)
        {
            _databaseFixture = databaseFixture;
        }
        
        [Theory]
        [InlineData(DbServer.SqlServer)]
        [InlineData(DbServer.Sqlite)]
        public void BulkInsertOrUpdate_EntityUsingBuiltInEnumToStringConverter_SavesToDatabase(DbServer dbServer)
        {
            using var db = new VcDbContext(_databaseFixture.GetOptions<VcDbContext>(dbServer, databaseName: $"{nameof(EFCoreBulkTest)}_ValueConverters"));
            using var _ = new EnsureCreatedAndDeleted(db.Database);

            db.BulkInsertOrUpdate(this.GetTestData().ToList());

            var connection = db.Database.GetDbConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM VcModels ORDER BY Id DESC";
            cmd.CommandType = System.Data.CommandType.Text;

            using var reader = cmd.ExecuteReader();
            reader.Read();

            var enumStr = reader.Field<string>("Enum");

            Assert.Equal(VcEnum.Hello.ToString(), enumStr);
        }

        [Theory]
        [InlineData(DbServer.SqlServer)]
        [InlineData(DbServer.Sqlite)]
        public void BatchUpdate_EntityUsingBuiltInEnumToStringConverter_UpdatesDatabaseWithEnumStringValue(DbServer dbServer)
        {
            using var db = new VcDbContext(_databaseFixture.GetOptions<VcDbContext>(dbServer, databaseName: $"{nameof(EFCoreBulkTest)}_ValueConverters"));
            using var _ = new EnsureCreatedAndDeleted(db.Database);

            db.BulkInsertOrUpdate(this.GetTestData().ToList());

            var date = new LocalDate(2020, 3, 21);
            db.VcModels.Where(x => x.LocalDate > date).BatchUpdate(x => new VcModel
            {
                Enum = VcEnum.Why
            });

            var connection = db.Database.GetDbConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM VcModels ORDER BY Id DESC";
            cmd.CommandType = System.Data.CommandType.Text;

            using var reader = cmd.ExecuteReader();
            reader.Read();

            var enumStr = reader.Field<string>("Enum");

            Assert.Equal(VcEnum.Why.ToString(), enumStr);
        }

        [Theory]
        [InlineData(DbServer.SqlServer)]
        [InlineData(DbServer.Sqlite)]
        public void BatchDelete_UsingWhereExpressionWithValueConverter_Deletes(DbServer dbServer)
        {
            using var db = new VcDbContext(_databaseFixture.GetOptions<VcDbContext>(dbServer, databaseName: $"{nameof(EFCoreBulkTest)}_ValueConverters"));
            using var _ = new EnsureCreatedAndDeleted(db.Database);

            db.BulkInsertOrUpdate(this.GetTestData().ToList());

            var date = new LocalDate(2020, 3, 21);
            db.VcModels.Where(x => x.LocalDate > date).BatchDelete();

            var models = db.VcModels.Count();
            Assert.Equal(0, models);
        }

        private IEnumerable<VcModel> GetTestData()
        {
            var one = new VcModel
            {
                Enum = VcEnum.Hello,
                LocalDate = new LocalDate(2021, 3, 22)
            };

            yield return one;
        }
    }
}
