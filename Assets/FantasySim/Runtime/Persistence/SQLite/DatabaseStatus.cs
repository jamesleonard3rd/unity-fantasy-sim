using System.Collections.Generic;

namespace FantasySim.Persistence.SQLite
{
    public sealed class DatabaseStatus
    {
        public DatabaseStatus(
            string databasePath,
            IReadOnlyList<string> appliedMigrations,
            IReadOnlyDictionary<string, int> tableCounts
        )
        {
            DatabasePath = databasePath;
            AppliedMigrations = appliedMigrations;
            TableCounts = tableCounts;
        }

        public string DatabasePath { get; }
        public IReadOnlyList<string> AppliedMigrations { get; }
        public IReadOnlyDictionary<string, int> TableCounts { get; }
    }
}
