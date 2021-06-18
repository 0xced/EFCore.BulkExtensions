﻿using EFCore.BulkExtensions.SqlAdapters;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EFCore.BulkExtensions.Tests
{
    [Collection("Database")]
    public class EFCoreBatchTestAsync
    {
        private readonly DatabaseFixture _databaseFixture;

        public EFCoreBatchTestAsync(DatabaseFixture databaseFixture)
        {
            _databaseFixture = databaseFixture;
        }
        
        protected int EntitiesNumber => 1000;

        [Theory]
        [InlineData(DbServer.SqlServer)]
        [InlineData(DbServer.Sqlite)]
        public async Task BatchTestAsync(DbServer dbServer)
        {
            await RunDeleteAllAsync(dbServer);
            await RunInsertAsync(dbServer);
            await RunBatchUpdateAsync(dbServer);

            int deletedEntities = 1;
            if (dbServer == DbServer.SqlServer)
            {
                deletedEntities = await RunTopBatchDeleteAsync(dbServer);
            }

            await RunBatchDeleteAsync(dbServer);

            await UpdateSettingAsync(dbServer, SettingsEnum.Sett1, "Val1UPDATE");
            await UpdateByteArrayToDefaultAsync(dbServer);

            using var context = new TestContext(_databaseFixture.GetOptions(dbServer));

            var firstItem = (await context.Items.ToListAsync()).First();
            var lastItem = (await context.Items.ToListAsync()).Last();
            Assert.Equal(1, deletedEntities);
            Assert.Equal(500, lastItem.ItemId);
            Assert.Equal("Updated", lastItem.Description);
            Assert.Null(lastItem.Price);
            Assert.StartsWith("name ", lastItem.Name);
            Assert.EndsWith(" Concatenated", lastItem.Name);

            if (dbServer == DbServer.SqlServer)
            {
                Assert.EndsWith(" TOP(1)", firstItem.Name);
            }
        }

        internal async Task RunDeleteAllAsync(DbServer dbServer)
        {
            using var context = new TestContext(_databaseFixture.GetOptions(dbServer));
            await context.Items.AddAsync(new Item { }); // used for initial add so that after RESEED it starts from 1, not 0
            await context.SaveChangesAsync();

            await context.Items.BatchDeleteAsync();
            await context.BulkDeleteAsync(context.Items.ToList());

            // RESET AutoIncrement
            string deleteTableSql = dbServer switch
            {
                DbServer.SqlServer => $"DBCC CHECKIDENT('[dbo].[{nameof(Item)}]', RESEED, 0);",
                DbServer.Sqlite => $"DELETE FROM sqlite_sequence WHERE name = '{nameof(Item)}';",
                _ => throw new ArgumentException($"Unknown database type: '{dbServer}'.", nameof(dbServer)),
            };
            context.Database.ExecuteSqlRaw(deleteTableSql);
        }

        private async Task RunInsertAsync(DbServer dbServer)
        {
            using (var context = new TestContext(_databaseFixture.GetOptions(dbServer)))
            {
                var entities = new List<Item>();
                for (int i = 1; i <= EntitiesNumber; i++)
                {
                    var entity = new Item
                    {
                        Name = "name " + i,
                        Description = "info " + Guid.NewGuid().ToString().Substring(0, 3),
                        Quantity = i % 10,
                        Price = i / (i % 5 + 1),
                        TimeUpdated = DateTime.Now,
                        ItemHistories = new List<ItemHistory>()
                    };
                    entities.Add(entity);
                }

                await context.Items.AddRangeAsync(entities);
                await context.SaveChangesAsync();
            }
        }

        private async Task RunBatchUpdateAsync(DbServer dbServer)
        {
            using var context = new TestContext(_databaseFixture.GetOptions(dbServer));

            //var updateColumns = new List<string> { nameof(Item.Quantity) }; // Adding explicitly PropertyName for update to its default value

            decimal price = 0;

            var query = context.Items.AsQueryable();
            if (dbServer == DbServer.SqlServer)
            {
                query = query.Where(a => a.ItemId <= 500 && a.Price >= price);
            }
            if (dbServer == DbServer.Sqlite)
            {
                query = query.Where(a => a.ItemId <= 500); // Sqlite currently does Not support multiple conditions
            }

            await query.BatchUpdateAsync(new Item { Description = "Updated" }/*, updateColumns*/);

            await query.BatchUpdateAsync(a => new Item { Name = a.Name + " Concatenated", Quantity = a.Quantity + 100, Price = null }); // example of BatchUpdate value Increment/Decrement

            if (dbServer == DbServer.SqlServer) // Sqlite currently does Not support Take(): LIMIT
            {
                query = context.Items.Where(a => a.ItemId <= 500 && a.Price == null);
                await query.Take(1).BatchUpdateAsync(a => new Item { Name = a.Name + " TOP(1)", Quantity = a.Quantity + 100 }); // example of BatchUpdate with TOP(1)

            }

            var list = new List<string>() { "Updated" };
            var updatedCount = await context.Set<Item>()
                                            .TagWith("From: someCallSite in someClassName") // To test parsing Sql with Tag leading comment
                                            .Where(a => list.Contains(a.Description))
                                            .BatchUpdateAsync(a => new Item() { TimeUpdated = DateTime.Now })
                                            .ConfigureAwait(false);

            if (dbServer == DbServer.SqlServer) // Sqlite Not supported
            {
                var newValue = 5;
                await context.Parents.Where(parent => parent.ParentId == 1)
                    .BatchUpdateAsync(parent => new Parent
                    {
                        Description = parent.Children.Where(child => child.IsEnabled && child.Value == newValue).Sum(child => child.Value).ToString(),
                        Value = newValue
                    })
                    .ConfigureAwait(false);
            }
        }

        private async Task<int> RunTopBatchDeleteAsync(DbServer dbServer)
        {
            using var context = new TestContext(_databaseFixture.GetOptions(dbServer));
            return await context.Items.Where(a => a.ItemId > 500).Take(1).BatchDeleteAsync();
        }

        private async Task RunBatchDeleteAsync(DbServer dbServer)
        {
            using var context = new TestContext(_databaseFixture.GetOptions(dbServer));
            await context.Items.Where(a => a.ItemId > 500).BatchDeleteAsync();
        }

        private async Task UpdateSettingAsync(DbServer dbServer, SettingsEnum settings, object value)
        {
            using var context = new TestContext(_databaseFixture.GetOptions(dbServer));

            await context.TruncateAsync<Setting>();

            await context.Settings.AddAsync(new Setting() { Settings = SettingsEnum.Sett1, Value = "Val1" }).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);

            // can work with explicit value: .Where(x => x.Settings == SettingsEnum.Sett1) or if named Parameter used then it has to be named (settings) same as Property (Settings) - Case not relevant, it is CaseInsensitive
            await context.Settings.Where(x => x.Settings == settings).BatchUpdateAsync(x => new Setting { Value = value.ToString() }).ConfigureAwait(false);

            await context.TruncateAsync<Setting>();
        }

        private async Task UpdateByteArrayToDefaultAsync(DbServer dbServer)
        {
            using var context = new TestContext(_databaseFixture.GetOptions(dbServer));

            await context.Files.BatchUpdateAsync(new File { DataBytes = null }, updateColumns: new List<string> { nameof(File.DataBytes) }).ConfigureAwait(false);
            await context.Files.BatchUpdateAsync(a => new File { DataBytes = null }).ConfigureAwait(false);
        }
    }
}
