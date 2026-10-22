using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SplitTogether.Data;

namespace SplitTogether.Services;

public static class DatabaseLocks
{
    // Stable across processes. A collision can only serialize unrelated operations;
    // it cannot weaken mutual exclusion. Transaction completion releases the lock.
    public static Task Acquire(AppDb db, string resource, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A transaction is required for a database lock.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("SplitTogether:" + resource));
        var key = BinaryPrimitives.ReadInt64BigEndian(hash);
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", ct);
    }
}
