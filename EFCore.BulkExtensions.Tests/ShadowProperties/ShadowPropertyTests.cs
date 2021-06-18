using EFCore.BulkExtensions.SqlAdapters;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EFCore.BulkExtensions.Tests.ShadowProperties
{
    [Collection("Database")]
    public class ShadowPropertyTests
    {
        private readonly DatabaseFixture _databaseFixture;

        public ShadowPropertyTests(DatabaseFixture databaseFixture)
        {
            _databaseFixture = databaseFixture;
        }

        [Theory]
        [InlineData(DbServer.SqlServer)]
        [InlineData(DbServer.Sqlite)]
        public void BulkInsertOrUpdate_EntityWithShadowProperties_SavesToDatabase(DbServer dbServer)
        {
            using var db = new SpDbContext(_databaseFixture.GetOptions<SpDbContext>(dbServer, databaseName: $"{nameof(EFCoreBulkTest)}_ShadowProperties"));
            using var _ = new EnsureCreatedAndDeleted(db.Database);

            db.BulkInsertOrUpdate(this.GetTestData(db).ToList(), new BulkConfig
            {
                EnableShadowProperties = true
            });

            var modelFromDb = db.SpModels.OrderByDescending(y => y.Id).First();
            Assert.Equal((long)10, db.Entry(modelFromDb).Property(SpModel.SpLong).CurrentValue);
            Assert.Null(db.Entry(modelFromDb).Property(SpModel.SpNullableLong).CurrentValue);

            Assert.Equal(new DateTime(2021, 02, 14), db.Entry(modelFromDb).Property(SpModel.SpDateTime).CurrentValue);
        }

        private IEnumerable<SpModel> GetTestData(DbContext db)
        {
            var one = new SpModel();
            db.Entry(one).Property(SpModel.SpLong).CurrentValue = (long)10;
            db.Entry(one).Property(SpModel.SpNullableLong).CurrentValue = null;
            db.Entry(one).Property(SpModel.SpDateTime).CurrentValue = new DateTime(2021, 02, 14);

            yield return one;
        }
    }
}
