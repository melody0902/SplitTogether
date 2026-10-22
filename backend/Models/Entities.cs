namespace SplitTogether.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Picture { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ExternalLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "Google";
    public string ProviderUserId { get; set; } = "";
}

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Currency { get; set; } = "TWD";
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}

public class GroupMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class GroupInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public class Expense
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Title { get; set; } = "";
    public long AmountMinor { get; set; }
    public DateOnly Date { get; set; }
    public Guid PayerId { get; set; }
    public string SplitMode { get; set; } = "Equal";
    public string Note { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public List<ExpenseSplit> Splits { get; set; } = [];
}

public class ExpenseSplit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExpenseId { get; set; }
    public Guid MemberId { get; set; }
    public long AmountMinor { get; set; }
}

// Retains membership references even when an expense's payer or participants are changed.
public class LedgerMemberHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid MemberId { get; set; }
}

public class Settlement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid FromId { get; set; }
    public Guid ToId { get; set; }
    public long AmountMinor { get; set; }
    public DateOnly Date { get; set; }
    public string Note { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
}

public class RequestRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public string Operation { get; set; } = "";
    public Guid ClientRequestId { get; set; }
    public string PayloadHash { get; set; } = "";
    public Guid ResultId { get; set; }
}
