using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitTogether.Data;
using SplitTogether.Models;
using SplitTogether.Services;

namespace SplitTogether.Controllers;

[ApiController, Authorize, Route("api/groups/{groupId:guid}")]
public class LedgerController(AppDb db, GroupAccess access) : ControllerBase
{
    Guid Uid => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    async Task<Expense> Expense(Guid group, Guid id, CancellationToken ct) =>
        await db
            .Expenses.Include(x => x.Splits)
            .SingleOrDefaultAsync(x => x.Id == id && x.GroupId == group && x.DeletedAt == null, ct)
        ?? throw new BusinessException(404, "not_found", "找不到支出");

    void Edit(Group g, Guid creator) =>
        LedgerMath.Require(
            g.OwnerId == Uid || creator == Uid,
            "只能管理自己建立的紀錄",
            "forbidden",
            403
        );

    async Task<List<SplitInput>> Validate(Guid group, ExpenseInput input, CancellationToken ct)
    {
        LedgerMath.Require(!string.IsNullOrWhiteSpace(input.Title), "請輸入支出標題");
        LedgerMath.Require(input.Date != default, "請選擇日期");
        var splits = LedgerMath.Split(input.AmountMinor, input.SplitMode, input.Splits);
        var members = await db
            .GroupMembers.Where(x => x.GroupId == group)
            .Select(x => x.Id)
            .ToListAsync(ct);
        LedgerMath.Require(
            members.Contains(input.PayerId) && splits.All(x => members.Contains(x.MemberId)),
            "付款人與分攤者必須是本群組成員"
        );
        return splits;
    }

    async Task<Guid?> Replay(
        Guid group,
        string operation,
        Guid key,
        object payload,
        CancellationToken ct
    )
    {
        LedgerMath.Require(key != Guid.Empty, "缺少 ClientRequestId");
        var hash = GroupsController.Hash(JsonSerializer.Serialize(payload));
        var old = await db.Requests.SingleOrDefaultAsync(
            x =>
                x.GroupId == group
                && x.UserId == Uid
                && x.Operation == operation
                && x.ClientRequestId == key,
            ct
        );
        if (old == null)
            return null;
        LedgerMath.Require(
            old.PayloadHash == hash,
            "相同請求編號不能使用不同內容",
            "idempotency_conflict",
            409
        );
        return old.ResultId;
    }

    void Record(Guid group, string operation, Guid key, object payload, Guid result) =>
        db.Requests.Add(
            new()
            {
                GroupId = group,
                UserId = Uid,
                Operation = operation,
                ClientRequestId = key,
                PayloadHash = GroupsController.Hash(JsonSerializer.Serialize(payload)),
                ResultId = result,
            }
        );

    [HttpGet("expenses")]
    public async Task<object> Expenses(
        Guid groupId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? payerId = null,
        CancellationToken ct = default
    )
    {
        await access.Read(groupId, Uid, ct);
        LedgerMath.Require(
            page >= 1 && page <= 100000 && pageSize >= 1 && pageSize <= 100,
            "分頁參數不正確"
        );
        LedgerMath.Require(search == null || search.Length <= 100, "搜尋內容過長");
        var q = db.Expenses.AsNoTracking().Where(x => x.GroupId == groupId && x.DeletedAt == null);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(x => x.Title.Contains(search));
        if (from != null)
            q = q.Where(x => x.Date >= from);
        if (to != null)
            q = q.Where(x => x.Date <= to);
        if (payerId != null)
            q = q.Where(x => x.PayerId == payerId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Splits)
            .ToListAsync(ct);
        return new
        {
            total,
            page,
            pageSize,
            items = items.Select(ExpenseDto.From),
        };
    }

    [HttpGet("expenses/{id:guid}")]
    public async Task<object> Get(Guid groupId, Guid id, CancellationToken ct)
    {
        await access.Read(groupId, Uid, ct);
        return ExpenseDto.From(await Expense(groupId, id, ct));
    }

    [HttpPost("expenses")]
    public async Task<object> Create(Guid groupId, ExpenseInput input, CancellationToken ct)
    {
        await using var tx = await access.Lock(groupId, ct);
        var (_, me) = await access.Read(groupId, Uid, ct);
        var replay = await Replay(groupId, "expense", input.ClientRequestId, input, ct);
        if (replay != null)
            return new { id = replay };
        var splits = await Validate(groupId, input, ct);
        var e = new Expense
        {
            GroupId = groupId,
            Title = input.Title.Trim(),
            AmountMinor = input.AmountMinor,
            Date = input.Date,
            PayerId = input.PayerId,
            SplitMode = input.SplitMode,
            Note = input.Note ?? "",
            CreatedBy = Uid,
            UpdatedBy = Uid,
        };
        e.Splits = splits
            .Select(s => new ExpenseSplit
            {
                ExpenseId = e.Id,
                MemberId = s.MemberId,
                AmountMinor = s.AmountMinor,
            })
            .ToList();
        db.Expenses.Add(e);
        Record(groupId, "expense", input.ClientRequestId, input, e.Id);
        await access.Remember(
            groupId,
            splits.Select(x => x.MemberId).Append(e.PayerId).Append(me.Id),
            ct
        );
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new { e.Id };
    }

    [HttpPut("expenses/{id:guid}")]
    public async Task<object> Update(
        Guid groupId,
        Guid id,
        ExpenseInput input,
        CancellationToken ct
    )
    {
        await using var tx = await access.Lock(groupId, ct);
        var (g, me) = await access.Read(groupId, Uid, ct);
        var e = await Expense(groupId, id, ct);
        Edit(g, e.CreatedBy);
        LedgerMath.Require(input.Version == e.Version, "支出已被修改，請重新載入", "conflict", 409);
        var splits = await Validate(groupId, input, ct);
        db.ExpenseSplits.RemoveRange(e.Splits);
        await db.SaveChangesAsync(ct);
        e.Title = input.Title.Trim();
        e.AmountMinor = input.AmountMinor;
        e.Date = input.Date;
        e.PayerId = input.PayerId;
        e.SplitMode = input.SplitMode;
        e.Note = input.Note ?? "";
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = Uid;
        e.Version = Guid.NewGuid();
        e.Splits = splits
            .Select(s => new ExpenseSplit
            {
                ExpenseId = e.Id,
                MemberId = s.MemberId,
                AmountMinor = s.AmountMinor,
            })
            .ToList();
        await access.Remember(
            groupId,
            splits.Select(x => x.MemberId).Append(e.PayerId).Append(me.Id),
            ct
        );
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ExpenseDto.From(e);
    }

    [HttpDelete("expenses/{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid groupId,
        Guid id,
        [FromQuery] Guid version,
        CancellationToken ct
    )
    {
        await using var tx = await access.Lock(groupId, ct);
        var (g, _) = await access.Read(groupId, Uid, ct);
        var e = await Expense(groupId, id, ct);
        Edit(g, e.CreatedBy);
        LedgerMath.Require(version == e.Version, "支出已被修改，請重新載入", "conflict", 409);
        e.DeletedAt = DateTime.UtcNow;
        e.UpdatedAt = DateTime.UtcNow;
        e.UpdatedBy = Uid;
        e.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("balances")]
    public async Task<object> Balances(Guid groupId, CancellationToken ct)
    {
        await using var tx = await access.Lock(groupId, ct);
        await access.Read(groupId, Uid, ct);
        return await access.Snapshot(groupId, ct);
    }

    [HttpGet("settlement-suggestions")]
    public async Task<object> Suggestions(Guid groupId, CancellationToken ct)
    {
        await using var tx = await access.Lock(groupId, ct);
        await access.Read(groupId, Uid, ct);
        return (await access.Snapshot(groupId, ct)).Suggestions;
    }

    [HttpGet("settlements")]
    public async Task<object> Settlements(Guid groupId, CancellationToken ct)
    {
        await access.Read(groupId, Uid, ct);
        var all = await db
            .Settlements.AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        return all.Select(SettlementDto.From);
    }

    [HttpPost("settlements")]
    public async Task<object> Pay(Guid groupId, SettlementInput input, CancellationToken ct)
    {
        await using var tx = await access.Lock(groupId, ct);
        var (g, me) = await access.Read(groupId, Uid, ct);
        var replay = await Replay(groupId, "settlement", input.ClientRequestId, input, ct);
        if (replay != null)
            return new { id = replay };
        LedgerMath.Amount(input.AmountMinor);
        LedgerMath.Require(input.Date != default, "請選擇日期");
        LedgerMath.Require(input.FromId != input.ToId, "付款人與收款人不可相同");
        var members = await db
            .GroupMembers.Where(x => x.GroupId == groupId)
            .Select(x => x.Id)
            .ToListAsync(ct);
        LedgerMath.Require(
            members.Contains(input.FromId) && members.Contains(input.ToId),
            "雙方必須是本群組成員"
        );
        LedgerMath.Require(
            g.OwnerId == Uid || me.Id == input.FromId || me.Id == input.ToId,
            "只能登記與自己相關的還款",
            "forbidden",
            403
        );
        var s = new Settlement
        {
            GroupId = groupId,
            FromId = input.FromId,
            ToId = input.ToId,
            AmountMinor = input.AmountMinor,
            Date = input.Date,
            Note = input.Note ?? "",
            CreatedBy = Uid,
        };
        db.Settlements.Add(s);
        Record(groupId, "settlement", input.ClientRequestId, input, s.Id);
        await access.Remember(groupId, [input.FromId, input.ToId, me.Id], ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new { s.Id };
    }

    [HttpPost("settlements/{id:guid}/void")]
    public async Task<IActionResult> Void(Guid groupId, Guid id, CancellationToken ct)
    {
        await using var tx = await access.Lock(groupId, ct);
        var (g, _) = await access.Read(groupId, Uid, ct);
        var s =
            await db.Settlements.SingleOrDefaultAsync(x => x.GroupId == groupId && x.Id == id, ct)
            ?? throw new BusinessException(404, "not_found", "找不到還款");
        Edit(g, s.CreatedBy);
        if (s.VoidedAt == null)
        {
            s.VoidedAt = DateTime.UtcNow;
            s.VoidedBy = Uid;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return NoContent();
    }
}
