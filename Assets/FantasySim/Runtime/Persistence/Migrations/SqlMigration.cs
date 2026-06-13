using System;
using System.Collections.Generic;

namespace FantasySim.Persistence.Migrations
{
    public sealed class SqlMigration
    {
        public SqlMigration(string id, string description, IReadOnlyList<string> statements)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Migration id is required.", nameof(id));
            }

            Id = id;
            Description = description ?? string.Empty;
            Statements = statements ?? throw new ArgumentNullException(nameof(statements));
        }

        public string Id { get; }
        public string Description { get; }
        public IReadOnlyList<string> Statements { get; }
    }
}
