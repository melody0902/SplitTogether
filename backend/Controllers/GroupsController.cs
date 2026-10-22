using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SplitTogether.Data;
using SplitTogether.Models;
using SplitTogether.Services;

namespace SplitTogether.Controllers;

[ApiController, Authorize, Route("api/groups")]
public class GroupsController(AppDb db, GroupAccess access) : ControllerBase
{
    Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<object> List(CancellationToken ct)
    {
        var ids = await db
            .GroupMembers.Where(x => x.UserId == UserId)
            .Select(x => x.GroupId)
            .ToListAsync(ct);
        var result = new List<object>();
        foreach (var id in ids)
        {
            await using var tx = await access.Lock(id, ct);
            var member = await db.GroupMembers.SingleOrDefaultAsync(
                x => x.GroupId == id && x.UserId == UserId,
                ct
            );
            if (member == null)
                continue;
            var g = await db.Groups.SingleAsync(x => x.Id == id, ct);
            var snapshot = await access.Snapshot(id, ct);
            result.Add(
                new
                {
                    g.Id,
                    g.Name,
                    g.Description,
                    g.Currency,
                    g.OwnerId,
                    g.Version,
                    netMinor = snapshot.Members.Single(x => x.MemberId == member.Id).NetMinor,
                }
            );
        }
        return result;
    }

    [HttpPost]
    public async Task<object> Create(GroupInput input, CancellationToken ct)
    {
        LedgerMath.Require(!string.IsNullOrWhiteSpace(input.Name), "請輸入群組名稱");
        var g = new Group
        {
            Name = input.Name.Trim(),
            Description = input.Description ?? "",
            OwnerId = UserId,
        };
        db.Groups.Add(g);
        db.GroupMembers.Add(new() { GroupId = g.Id, UserId = UserId });
        await db.SaveChangesAsync(ct);
        return new { g.Id };
    }

    [HttpGet("{id:guid}")]
    public async Task<object> Get(Guid id, CancellationToken ct)
    {
        var (g, m) = await access.Read(id, UserId, ct);
        return new
        {
            g.Id,
            g.Name,
            g.Description,
            g.Currency,
            g.OwnerId,
            g.Version,
            myMemberId = m.Id,
        };
    }

    [HttpPatch("{id:guid}")]
    public async Task<object> Update(Guid id, GroupInput input, CancellationToken ct)
    {
        await using var tx = await access.Lock(id, ct);
        var (g, _) = await access.Read(id, UserId, ct);
        GroupAccess.Owner(g, UserId);
        LedgerMath.Require(input.Version == g.Version, "群組已被修改，請重新整理", "conflict", 409);
        LedgerMath.Require(!string.IsNullOrWhiteSpace(input.Name), "請輸入名稱");
        g.Name = input.Name.Trim();
        g.Description = input.Description ?? "";
        g.Version = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new { g.Version };
    }

    [HttpGet("{id:guid}/members")]
    public async Task<object> Members(Guid id, CancellationToken ct)
    {
        await access.Read(id, UserId, ct);
        return await (
            from m in db.GroupMembers
            join u in db.Users on m.UserId equals u.Id
            where m.GroupId == id
            select new
            {
                m.Id,
                m.UserId,
                u.Name,
                u.Picture,
                hasHistory = db.LedgerMemberHistory.Any(h => h.MemberId == m.Id),
            }
        ).ToListAsync(ct);
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> Remove(Guid id, Guid memberId, CancellationToken ct)
    {
        await using var tx = await access.Lock(id, ct);
        var (g, _) = await access.Read(id, UserId, ct);
        GroupAccess.Owner(g, UserId);
        var m =
            await db.GroupMembers.SingleOrDefaultAsync(x => x.Id == memberId && x.GroupId == id, ct)
            ?? throw new BusinessException(404, "not_found", "找不到成員");
        LedgerMath.Require(m.UserId != g.OwnerId, "無法移除擁有者");
        LedgerMath.Require(
            !await db.LedgerMemberHistory.AnyAsync(x => x.MemberId == memberId, ct),
            "成員已有歷史帳務，無法移除",
            "member_has_history",
            409
        );
        db.GroupMembers.Remove(m);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/invitations")]
    public async Task<object> Invitations(Guid id, CancellationToken ct)
    {
        var (g, _) = await access.Read(id, UserId, ct);
        GroupAccess.Owner(g, UserId);
        return await db
            .GroupInvitations.Where(x => x.GroupId == id)
            .Select(x => new
            {
                x.Id,
                x.ExpiresAt,
                x.RevokedAt,
            })
            .ToListAsync(ct);
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<object> Invite(Guid id, CancellationToken ct)
    {
        await using var tx = await access.Lock(id, ct);
        var (g, _) = await access.Read(id, UserId, ct);
        GroupAccess.Owner(g, UserId);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invite = new GroupInvitation
        {
            GroupId = id,
            TokenHash = Hash(token),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        };
        db.GroupInvitations.Add(invite);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new
        {
            invite.Id,
            invite.ExpiresAt,
            token,
        };
    }

    [HttpDelete("{id:guid}/invitations/{inviteId:guid}")]
    public async Task<IActionResult> Revoke(Guid id, Guid inviteId, CancellationToken ct)
    {
        await using var tx = await access.Lock(id, ct);
        var (g, _) = await access.Read(id, UserId, ct);
        GroupAccess.Owner(g, UserId);
        var invite =
            await db.GroupInvitations.SingleOrDefaultAsync(
                x => x.GroupId == id && x.Id == inviteId,
                ct
            ) ?? throw new BusinessException(404, "not_found", "找不到邀請");
        invite.RevokedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    internal static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

[ApiController, Route("api/invitations"), EnableRateLimiting("invites")]
public class InvitationsController(AppDb db, GroupAccess access) : ControllerBase
{
    async Task<GroupInvitation> Find(string token, CancellationToken ct)
    {
        LedgerMath.Require(token.Length == 64, "邀請無效或已失效", "invalid_invite", 404);
        var hash = GroupsController.Hash(token);
        return await db.GroupInvitations.SingleOrDefaultAsync(x => x.TokenHash == hash, ct)
            ?? throw new BusinessException(404, "invalid_invite", "邀請無效或已失效");
    }

    static void Valid(GroupInvitation i) =>
        LedgerMath.Require(
            i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow,
            "邀請已過期或撤銷",
            "invalid_invite",
            404
        );

    [HttpGet("{token}")]
    public async Task<object> Info(string token, CancellationToken ct)
    {
        var i = await Find(token, ct);
        Valid(i);
        var g = await db.Groups.SingleAsync(x => x.Id == i.GroupId, ct);
        return new { groupName = g.Name, i.ExpiresAt };
    }

    [Authorize, HttpPost("{token}/accept")]
    public async Task<object> Accept(string token, CancellationToken ct)
    {
        var i = await Find(token, ct);
        await using var tx = await access.Lock(i.GroupId, ct);
        await db.Entry(i).ReloadAsync(ct);
        Valid(i);
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!await db.GroupMembers.AnyAsync(x => x.GroupId == i.GroupId && x.UserId == uid, ct))
        {
            db.GroupMembers.Add(new() { GroupId = i.GroupId, UserId = uid });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new { groupId = i.GroupId };
    }
}
