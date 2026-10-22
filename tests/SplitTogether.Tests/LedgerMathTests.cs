using SplitTogether.Models;
using SplitTogether.Services;
using Xunit;

namespace SplitTogether.Tests;

public class LedgerMathTests
{
    static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001"),
        B = Guid.Parse("00000000-0000-0000-0000-000000000002"),
        C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    static readonly (Guid, string)[] Members = [(A, "A"), (B, "B"), (C, "C")];

    static Expense Expense(Guid payer, long amount, params Guid[] ids) =>
        new()
        {
            PayerId = payer,
            AmountMinor = amount,
            Splits = LedgerMath
                .Split(amount, "Equal", ids.Select(x => new SplitInput(x, 0)).ToArray())
                .Select(x => new ExpenseSplit
                {
                    MemberId = x.MemberId,
                    AmountMinor = x.AmountMinor,
                })
                .ToList(),
        };

    [Theory]
    [InlineData(900, 300, 300, 300)]
    [InlineData(10000, 3334, 3333, 3333)]
    [InlineData(1, 1, 0, 0)]
    public void EqualRemainder(long amount, long a, long b, long c)
    {
        var s = LedgerMath.Split(amount, "Equal", [new(C, 0), new(A, 0), new(B, 0)]);
        Assert.Equal(new[] { a, b, c }, s.Select(x => x.AmountMinor));
        Assert.Equal(new[] { A, B, C }, s.Select(x => x.MemberId));
    }

    [Fact]
    public void CustomExact()
    {
        Assert.Equal(
            100,
            LedgerMath.Split(100, "Custom", [new(A, 0), new(B, 100)]).Sum(x => x.AmountMinor)
        );
    }

    [Fact]
    public void InvalidSplits()
    {
        Assert.Throws<BusinessException>(() => LedgerMath.Split(100, "Custom", [new(A, 99)]));
        Assert.Throws<BusinessException>(() =>
            LedgerMath.Split(100, "Custom", [new(A, -1), new(B, 101)])
        );
        Assert.Throws<BusinessException>(() =>
            LedgerMath.Split(100, "Equal", [new(A, 0), new(A, 0)])
        );
        Assert.Throws<BusinessException>(() => LedgerMath.Split(100, "Equal", []));
        Assert.Throws<BusinessException>(() => LedgerMath.Split(100, "Unknown", [new(A, 0)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100000000001)]
    [InlineData(long.MaxValue)]
    public void InvalidAmount(long n) =>
        Assert.Throws<BusinessException>(() => LedgerMath.Amount(n));

    [Fact]
    public void MaximumAmount()
    {
        LedgerMath.Amount(LedgerMath.MaxAmount);
        Assert.Throws<BusinessException>(() => LedgerMath.Safe(LedgerMath.MaxSafe + 1));
    }

    [Fact]
    public void PayerNotInSplit()
    {
        var l = LedgerMath.Calculate(Members, [Expense(A, 100, B)], []);
        Assert.Equal(new long[] { 100, -100, 0 }, l.Members.Select(x => x.NetMinor));
    }

    [Fact]
    public void RequiredScenarioAndVoid()
    {
        var expenses = new[] { Expense(A, 90000, A, B, C), Expense(B, 30000, B, C) };
        var l = LedgerMath.Calculate(Members, expenses, []);
        Assert.Equal(new long[] { 60000, -15000, -45000 }, l.Members.Select(x => x.NetMinor));
        Assert.Equal(120000, l.TotalMinor);
        var settlements = new[]
        {
            new Settlement
            {
                FromId = B,
                ToId = A,
                AmountMinor = 15000,
            },
            new Settlement
            {
                FromId = C,
                ToId = A,
                AmountMinor = 45000,
            },
        };
        Assert.All(
            LedgerMath.Calculate(Members, expenses, settlements).Members,
            m => Assert.Equal(0, m.NetMinor)
        );
        settlements[0].VoidedAt = DateTime.UtcNow;
        Assert.Equal(
            new long[] { 15000, -15000, 0 },
            LedgerMath.Calculate(Members, expenses, settlements).Members.Select(x => x.NetMinor)
        );
    }

    [Fact]
    public void PartialOverpaymentAndDeletedExpense()
    {
        var e = Expense(A, 10000, B);
        var s = new Settlement
        {
            FromId = B,
            ToId = A,
            AmountMinor = 3000,
        };
        Assert.Equal(7000, LedgerMath.Calculate(Members, [e], [s]).Members[0].NetMinor);
        s.AmountMinor = 15000;
        Assert.Equal(-5000, LedgerMath.Calculate(Members, [e], [s]).Members[0].NetMinor);
        e.DeletedAt = DateTime.UtcNow;
        Assert.Equal(-15000, LedgerMath.Calculate(Members, [e], [s]).Members[0].NetMinor);
        s.VoidedAt = DateTime.UtcNow;
        Assert.All(
            LedgerMath.Calculate(Members, [e], [s]).Members,
            m => Assert.Equal(0, m.NetMinor)
        );
    }

    [Fact]
    public void EditedExpenseRetainsRepayment()
    {
        var e = Expense(A, 10000, B);
        var s = new Settlement
        {
            FromId = B,
            ToId = A,
            AmountMinor = 10000,
        };
        e.AmountMinor = 5000;
        e.Splits[0].AmountMinor = 5000;
        Assert.Equal(
            new long[] { -5000, 5000, 0 },
            LedgerMath.Calculate(Members, [e], [s]).Members.Select(x => x.NetMinor)
        );
    }

    [Fact]
    public void GeneratedLedgersConserveAndSettle()
    {
        var random = new Random(97);
        for (var n = 0; n < 200; n++)
        {
            var es = Enumerable
                .Range(0, 15)
                .Select(_ =>
                    Expense(Members[random.Next(3)].Item1, random.Next(1, 1000000), A, B, C)
                )
                .ToArray();
            var snapshot = LedgerMath.Calculate(Members, es, []);
            Assert.Equal(0, snapshot.Members.Sum(x => x.NetMinor));
            var payments = snapshot
                .Suggestions.Select(s => new Settlement
                {
                    FromId = s.FromId,
                    ToId = s.ToId,
                    AmountMinor = s.AmountMinor,
                })
                .ToArray();
            Assert.All(
                LedgerMath.Calculate(Members, es, payments).Members,
                m => Assert.Equal(0, m.NetMinor)
            );
            Assert.Equal(
                snapshot.Suggestions,
                LedgerMath
                    .Calculate(Members.Reverse().ToArray(), es.Reverse().ToArray(), [])
                    .Suggestions
            );
        }
    }
}
