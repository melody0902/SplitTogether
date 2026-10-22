using System.ComponentModel.DataAnnotations;
using SplitTogether.Services;

namespace SplitTogether.Models;

public record GroupInput(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [StringLength(500)] string Description,
    Guid? Version
);

public record ExpenseInput(
    [Required, StringLength(100, MinimumLength = 1)] string Title,
    long AmountMinor,
    DateOnly Date,
    Guid PayerId,
    [Required] string SplitMode,
    [Required] List<SplitInput> Splits,
    [StringLength(1000)] string Note,
    Guid ClientRequestId,
    Guid? Version
);

public record SettlementInput(
    Guid FromId,
    Guid ToId,
    long AmountMinor,
    DateOnly Date,
    [StringLength(1000)] string Note,
    Guid ClientRequestId
);

public record ExpenseDto(
    Guid Id,
    Guid GroupId,
    string Title,
    long AmountMinor,
    DateOnly Date,
    Guid PayerId,
    string SplitMode,
    string Note,
    Guid CreatedBy,
    Guid UpdatedBy,
    DateTime CreatedAt,
    Guid Version,
    IReadOnlyList<SplitInput> Splits
)
{
    public static ExpenseDto From(Expense e) =>
        new(
            e.Id,
            e.GroupId,
            e.Title,
            e.AmountMinor,
            e.Date,
            e.PayerId,
            e.SplitMode,
            e.Note,
            e.CreatedBy,
            e.UpdatedBy,
            e.CreatedAt,
            e.Version,
            e.Splits.Select(s => new SplitInput(s.MemberId, s.AmountMinor)).ToArray()
        );
}

public record SettlementDto(
    Guid Id,
    Guid FromId,
    Guid ToId,
    long AmountMinor,
    DateOnly Date,
    string Note,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime? VoidedAt,
    Guid? VoidedBy
)
{
    public static SettlementDto From(Settlement s) =>
        new(
            s.Id,
            s.FromId,
            s.ToId,
            s.AmountMinor,
            s.Date,
            s.Note,
            s.CreatedBy,
            s.CreatedAt,
            s.VoidedAt,
            s.VoidedBy
        );
}
