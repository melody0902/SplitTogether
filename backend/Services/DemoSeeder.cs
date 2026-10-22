using Microsoft.EntityFrameworkCore;
using SplitTogether.Data;
using SplitTogether.Models;

namespace SplitTogether.Services;

public static class DemoSeeder
{
    public static async Task Run(AppDb db, string environment)
    {
        if (environment is not ("Development" or "Testing"))
            throw new InvalidOperationException(
                "Demo seed is allowed only in Development or Testing."
            );
        var id = Guid.Parse("de000000-0000-0000-0000-000000000001");
        await using var tx = await db.Database.BeginTransactionAsync();
        await DatabaseLocks.Acquire(db, "group:" + id.ToString("D"), CancellationToken.None);
        if (await db.Groups.AnyAsync(g => g.Id == id))
        {
            Console.WriteLine("示範群組已存在，未新增重複資料。");
            return;
        }
        var users = await db
            .Users.Where(u =>
                db.ExternalLogins.Any(l => l.UserId == u.Id && l.Provider == "Google")
            )
            .OrderBy(u => u.CreatedAt)
            .Take(3)
            .ToListAsync();
        if (users.Count < 3)
            throw new InvalidOperationException(
                "請先讓三位真實 Google 使用者登入，再執行示範 seed。此功能不建立假登入。"
            );
        var group = new Group
        {
            Id = id,
            Name = "示範：三人旅行",
            Description = "A 代墊 900，B 為 B/C 代墊 300。",
            OwnerId = users[0].Id,
        };
        db.Groups.Add(group);
        var members = users.Select(u => new GroupMember { GroupId = id, UserId = u.Id }).ToArray();
        db.GroupMembers.AddRange(members);
        await db.SaveChangesAsync();
        Expense Make(string title, long amount, int payer, params int[] participants)
        {
            var e = new Expense
            {
                GroupId = id,
                Title = title,
                AmountMinor = amount,
                Date = DateOnly.FromDateTime(DateTime.Today),
                PayerId = members[payer].Id,
                CreatedBy = users[payer].Id,
                UpdatedBy = users[payer].Id,
            };
            e.Splits = LedgerMath
                .Split(
                    amount,
                    "Equal",
                    participants.Select(i => new SplitInput(members[i].Id, 0)).ToArray()
                )
                .Select(s => new ExpenseSplit
                {
                    ExpenseId = e.Id,
                    MemberId = s.MemberId,
                    AmountMinor = s.AmountMinor,
                })
                .ToList();
            return e;
        }
        db.Expenses.AddRange(Make("三人晚餐", 90000, 0, 0, 1, 2), Make("兩人午餐", 30000, 1, 1, 2));
        db.LedgerMemberHistory.AddRange(
            members.Select(m => new LedgerMemberHistory { GroupId = id, MemberId = m.Id })
        );
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        Console.WriteLine("已建立示範群組。");
    }
}
