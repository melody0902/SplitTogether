using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SplitTogether.Data;
using SplitTogether.Models;

namespace SplitTogether.Services;

public class GroupAccess(AppDb db)
{
    public async Task<(Group Group, GroupMember Member)> Read(
        Guid id,
        Guid user,
        CancellationToken ct
    )
    {
        var member =
            await db.GroupMembers.SingleOrDefaultAsync(x => x.GroupId == id && x.UserId == user, ct)
            ?? throw new BusinessException(404, "not_found", "找不到群組");
        return (await db.Groups.SingleAsync(x => x.Id == id, ct), member);
    }

    public static void Owner(Group g, Guid user) =>
        LedgerMath.Require(g.OwnerId == user, "需要群組擁有者權限", "forbidden", 403);

    public async Task<IDbContextTransaction> Lock(Guid id, CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await DatabaseLocks.Acquire(db, "group:" + id.ToString("D"), ct);
            return tx;
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
    }

    public async Task<LedgerSnapshot> Snapshot(Guid id, CancellationToken ct)
    {
        var members = await (
            from m in db.GroupMembers
            join u in db.Users on m.UserId equals u.Id
            where m.GroupId == id
            select new { m.Id, u.Name }
        ).ToListAsync(ct);
        var expenses = await db
            .Expenses.AsNoTracking()
            .Include(x => x.Splits)
            .Where(x => x.GroupId == id && x.DeletedAt == null)
            .ToListAsync(ct);
        var settlements = await db
            .Settlements.AsNoTracking()
            .Where(x => x.GroupId == id && x.VoidedAt == null)
            .ToListAsync(ct);
        return LedgerMath.Calculate(
            members.Select(x => (x.Id, x.Name)).ToArray(),
            expenses,
            settlements
        );
    }

    public async Task Remember(Guid group, IEnumerable<Guid> members, CancellationToken ct)
    {
        var existing = await db
            .LedgerMemberHistory.Where(x => x.GroupId == group)
            .Select(x => x.MemberId)
            .ToListAsync(ct);
        foreach (var id in members.Distinct().Except(existing))
            db.LedgerMemberHistory.Add(new() { GroupId = group, MemberId = id });
    }
}
