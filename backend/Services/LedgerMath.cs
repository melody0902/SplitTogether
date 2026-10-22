using SplitTogether.Models;

namespace SplitTogether.Services;

public class BusinessException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public record SplitInput(Guid MemberId, long AmountMinor);

public record MemberBalance(
    Guid MemberId,
    string Name,
    long PaidMinor,
    long ShareMinor,
    long SentMinor,
    long ReceivedMinor,
    long NetMinor
);

public record Suggestion(Guid FromId, Guid ToId, long AmountMinor);

public record LedgerSnapshot(
    long TotalMinor,
    IReadOnlyList<MemberBalance> Members,
    IReadOnlyList<Suggestion> Suggestions
);

public static class LedgerMath
{
    public const long MaxAmount = 100_000_000_000,
        MaxSafe = 9_007_199_254_740_991;

    public static void Require(
        bool ok,
        string message,
        string code = "validation",
        int status = 400
    )
    {
        if (!ok)
            throw new BusinessException(status, code, message);
    }

    public static long Safe(long amount)
    {
        Require(
            amount >= -MaxSafe && amount <= MaxSafe,
            "金額超出可安全處理範圍",
            "amount_overflow"
        );
        return amount;
    }

    public static void Amount(long amount) =>
        Require(amount > 0 && amount <= MaxAmount, "金額必須大於零，且不超過十億元");

    public static List<SplitInput> Split(long amount, string mode, IReadOnlyList<SplitInput> inputs)
    {
        Amount(amount);
        Require(
            inputs.Count > 0 && inputs.Select(x => x.MemberId).Distinct().Count() == inputs.Count,
            "分攤成員不可為空或重複"
        );
        var sorted = inputs
            .OrderBy(x => x.MemberId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (mode == "Equal")
            return sorted
                .Select(
                    (x, i) =>
                        new SplitInput(
                            x.MemberId,
                            amount / sorted.Length + (i < amount % sorted.Length ? 1 : 0)
                        )
                )
                .ToList();
        Require(mode == "Custom", "無效的分攤方式");
        Require(
            sorted.All(x => x.AmountMinor >= 0 && x.AmountMinor <= MaxAmount),
            "自訂金額不可為負數或超過上限"
        );
        Require(sorted.Sum(x => (decimal)x.AmountMinor) == amount, "分攤合計必須等於總金額");
        return sorted.ToList();
    }

    public static LedgerSnapshot Calculate(
        IReadOnlyList<(Guid Id, string Name)> members,
        IReadOnlyList<Expense> expenses,
        IReadOnlyList<Settlement> settlements
    )
    {
        var active = expenses.Where(x => x.DeletedAt == null).ToArray();
        var payments = settlements.Where(x => x.VoidedAt == null).ToArray();
        long Sum(IEnumerable<long> amounts)
        {
            long total = 0;
            foreach (var n in amounts)
                total = checked(total + n);
            return Safe(total);
        }
        var rows = members
            .Select(m =>
            {
                var paid = Sum(active.Where(x => x.PayerId == m.Id).Select(x => x.AmountMinor));
                var share = Sum(
                    active
                        .SelectMany(x => x.Splits)
                        .Where(x => x.MemberId == m.Id)
                        .Select(x => x.AmountMinor)
                );
                var sent = Sum(payments.Where(x => x.FromId == m.Id).Select(x => x.AmountMinor));
                var received = Sum(payments.Where(x => x.ToId == m.Id).Select(x => x.AmountMinor));
                return new MemberBalance(
                    m.Id,
                    m.Name,
                    paid,
                    share,
                    sent,
                    received,
                    Safe(checked(paid - share + sent - received))
                );
            })
            .ToArray();
        Require(
            rows.Sum(x => (decimal)x.NetMinor) == 0,
            "帳務不平衡，請聯絡管理者",
            "ledger_invalid",
            500
        );
        return new(Sum(active.Select(x => x.AmountMinor)), rows, Suggest(rows));
    }

    public static List<Suggestion> Suggest(IEnumerable<MemberBalance> rows)
    {
        var creditors = rows.Where(x => x.NetMinor > 0)
            .OrderByDescending(x => x.NetMinor)
            .ThenBy(x => x.MemberId.ToString("D"), StringComparer.Ordinal)
            .Select(x => (x.MemberId, Amount: x.NetMinor))
            .ToArray();
        var debtors = rows.Where(x => x.NetMinor < 0)
            .OrderBy(x => x.NetMinor)
            .ThenBy(x => x.MemberId.ToString("D"), StringComparer.Ordinal)
            .Select(x => (x.MemberId, Amount: -x.NetMinor))
            .ToArray();
        var result = new List<Suggestion>();
        int i = 0,
            j = 0;
        while (i < debtors.Length && j < creditors.Length)
        {
            var n = Math.Min(debtors[i].Amount, creditors[j].Amount);
            result.Add(new(debtors[i].MemberId, creditors[j].MemberId, n));
            debtors[i].Amount -= n;
            creditors[j].Amount -= n;
            if (debtors[i].Amount == 0)
                i++;
            if (creditors[j].Amount == 0)
                j++;
        }
        return result;
    }
}
