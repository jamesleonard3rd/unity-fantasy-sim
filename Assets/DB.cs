using FantasySim.Persistence.SQLite;
using UnityEngine;

public class DatabaseTest : MonoBehaviour
{
    void Start()
    {
        var initializer = new DatabaseInitializer();
        var appliedMigrations = initializer.Initialize();

        Debug.Log(
            $"Database ready at: {initializer.DatabasePath}. " +
            $"Applied {appliedMigrations.Count} migration(s)."
        );
    }
}
