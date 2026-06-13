using SQLite;

namespace FantasySim.Persistence.SQLite
{
    public sealed class SqliteConnectionFactory
    {
        private readonly DatabasePathProvider pathProvider;
        private readonly int busyTimeoutMilliseconds;

        public SqliteConnectionFactory(
            DatabasePathProvider pathProvider,
            int busyTimeoutMilliseconds = 5000
        )
        {
            this.pathProvider = pathProvider;
            this.busyTimeoutMilliseconds = busyTimeoutMilliseconds;
        }

        public SQLiteConnection OpenConnection()
        {
            pathProvider.EnsureDirectoryExists();

            var connection = new SQLiteConnection(pathProvider.DatabasePath);
            connection.Execute("PRAGMA foreign_keys = ON");
            connection.Execute("PRAGMA journal_mode = WAL");
            connection.Execute($"PRAGMA busy_timeout = {busyTimeoutMilliseconds}");

            return connection;
        }
    }
}
