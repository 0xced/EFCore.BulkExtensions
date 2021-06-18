using EFCore.BulkExtensions.SqlAdapters;
using Xunit;

namespace EFCore.BulkExtensions.Tests
{
    [Collection("Database")]
    public class BatchUtilTests
    {
        private readonly DatabaseFixture _databaseFixture;

        public BatchUtilTests(DatabaseFixture databaseFixture)
        {
            _databaseFixture = databaseFixture;
        }
        
        [Fact]
        public void GetBatchSql_UpdateSqlite_ReturnsExpectedValues()
        {
            using var context = new TestContext(_databaseFixture.GetOptions(DbServer.Sqlite));
            (string sql, string tableAlias, string tableAliasSufixAs, _, _, _) = BatchUtil.GetBatchSql(context.Items, context, true);

            Assert.Equal("\"Item\"", tableAlias);
            Assert.Equal(" AS \"i\"", tableAliasSufixAs);
        }
    }
}
