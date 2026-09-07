using System;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FemVoice.Tests.Portable;

/// <summary>
/// Pins the SQLite engine that actually ships inside the app.
///
/// The build used to emit NU1903: SQLitePCLRaw.lib.e_sqlite3 2.1.6 bundled a SQLite affected by
/// CVE-2025-6965 (GHSA-2m69-gcr7-jv3q, High) — a memory-corruption bug reachable through crafted SQL or a
/// crafted database file. That matters here rather than being theoretical: the cross-device merge feature
/// opens a SQLite file the USER points at, so an untrusted database can reach the engine.
///
/// Checking the package version alone is not enough — a package version tells you what NuGet resolved, not
/// what the native library actually is. This asks the engine itself, so a resolution change, a downgrade or a
/// transitive pin that quietly reintroduces an old engine fails here instead of shipping.
/// </summary>
public class BundledSqliteVersionTests
{
    /// <summary>CVE-2025-6965 is fixed in SQLite 3.50.2. Anything older is vulnerable.</summary>
    private static readonly Version MinimumSafeSqlite = new(3, 50, 2);

    [Fact]
    public void BundledEngineIsAtLeastTheVersionThatFixesCve_2025_6965()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version()";
        string reported = (string)command.ExecuteScalar()!;

        Assert.True(Version.TryParse(reported, out var actual),
            $"sqlite_version() returned something unparseable: '{reported}'");

        Assert.True(actual >= MinimumSafeSqlite,
            $"The bundled SQLite is {actual}, older than {MinimumSafeSqlite}, which is affected by " +
            "CVE-2025-6965. Raise the Microsoft.Data.Sqlite / SQLitePCLRaw version rather than relaxing this test.");
    }

    [Fact]
    public void EngineStillWorksForTheOperationsTheAppRelIesOn()
    {
        // A version bump that broke basic SQL would otherwise only show up at runtime on a user's machine.
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE t (Id INTEGER PRIMARY KEY, StartTime TEXT, Score REAL);
            INSERT INTO t (StartTime, Score) VALUES ('2026-09-07T10:00:00.0000000Z', 71.5);
            INSERT INTO t (StartTime, Score) VALUES ('2026-09-06T10:00:00.0000000Z', 64.0);";
        cmd.ExecuteNonQuery();

        // PRAGMA table_info drives the merge feature's schema-drift tolerance.
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('t')";
        Assert.Equal(3L, (long)cmd.ExecuteScalar()!);

        // ISO-8601 round-trip strings must still order and compare as text, as the session tables assume.
        cmd.CommandText = "SELECT StartTime FROM t ORDER BY StartTime DESC LIMIT 1";
        Assert.Equal("2026-09-07T10:00:00.0000000Z", (string)cmd.ExecuteScalar()!);

        cmd.CommandText = "SELECT ROUND(AVG(Score), 2) FROM t";
        Assert.Equal(67.75, Convert.ToDouble(cmd.ExecuteScalar()), 2);
    }
}
