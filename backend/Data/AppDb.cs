using Microsoft.EntityFrameworkCore;
using SplitTogether.Models;

namespace SplitTogether.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupInvitation> GroupInvitations => Set<GroupInvitation>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseSplit> ExpenseSplits => Set<ExpenseSplit>();
    public DbSet<Settlement> Settlements => Set<Settlement>();
    public DbSet<RequestRecord> Requests => Set<RequestRecord>();
    public DbSet<LedgerMemberHistory> LedgerMemberHistory => Set<LedgerMemberHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().Property(x => x.Name).HasMaxLength(200);
        b.Entity<User>().Property(x => x.Email).HasMaxLength(320);
        b.Entity<User>().Property(x => x.Picture).HasMaxLength(2048);
        b.Entity<ExternalLogin>().Property(x => x.Provider).HasMaxLength(40);
        b.Entity<ExternalLogin>().Property(x => x.ProviderUserId).HasMaxLength(200);
        b.Entity<ExternalLogin>().HasIndex(x => new { x.Provider, x.ProviderUserId }).IsUnique();
        b.Entity<ExternalLogin>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<Group>().Property(x => x.Name).HasMaxLength(100);
        b.Entity<Group>().Property(x => x.Description).HasMaxLength(500);
        b.Entity<Group>().Property(x => x.Currency).HasMaxLength(3);
        b.Entity<Group>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Group>().HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId);
        b.Entity<GroupMember>().HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
        b.Entity<GroupMember>().HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<GroupMember>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<GroupInvitation>().Property(x => x.TokenHash).HasMaxLength(64);
        b.Entity<GroupInvitation>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<GroupInvitation>().HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<Expense>().Property(x => x.Title).HasMaxLength(100);
        b.Entity<Expense>().Property(x => x.Note).HasMaxLength(1000);
        b.Entity<Expense>().Property(x => x.SplitMode).HasMaxLength(10);
        b.Entity<Expense>().Property(x => x.Version).IsConcurrencyToken();
        b.Entity<Expense>()
            .HasIndex(x => new
            {
                x.GroupId,
                x.Date,
                x.Id,
            });
        b.Entity<Expense>().HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<Expense>().HasOne<GroupMember>().WithMany().HasForeignKey(x => x.PayerId);
        b.Entity<Expense>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy);
        b.Entity<Expense>().HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy);
        b.Entity<Expense>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Expense_Amount",
                    "\"AmountMinor\" > 0 AND \"AmountMinor\" <= 100000000000"
                );
                t.HasCheckConstraint("CK_Expense_Mode", "\"SplitMode\" IN ('Equal','Custom')");
            });
        b.Entity<ExpenseSplit>()
            .HasOne<Expense>()
            .WithMany(x => x.Splits)
            .HasForeignKey(x => x.ExpenseId);
        b.Entity<ExpenseSplit>().HasOne<GroupMember>().WithMany().HasForeignKey(x => x.MemberId);
        b.Entity<ExpenseSplit>().HasIndex(x => new { x.ExpenseId, x.MemberId }).IsUnique();
        b.Entity<ExpenseSplit>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "CK_Split_Amount",
                    "\"AmountMinor\" >= 0 AND \"AmountMinor\" <= 100000000000"
                )
            );
        b.Entity<Settlement>().Property(x => x.Note).HasMaxLength(1000);
        b.Entity<Settlement>().HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<Settlement>().HasOne<GroupMember>().WithMany().HasForeignKey(x => x.FromId);
        b.Entity<Settlement>().HasOne<GroupMember>().WithMany().HasForeignKey(x => x.ToId);
        b.Entity<Settlement>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy);
        b.Entity<Settlement>().HasOne<User>().WithMany().HasForeignKey(x => x.VoidedBy);
        b.Entity<Settlement>()
            .HasIndex(x => new
            {
                x.GroupId,
                x.Date,
                x.Id,
            });
        b.Entity<Settlement>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Settlement_Amount",
                    "\"AmountMinor\" > 0 AND \"AmountMinor\" <= 100000000000"
                );
                t.HasCheckConstraint("CK_Settlement_Parties", "\"FromId\" <> \"ToId\"");
            });
        b.Entity<LedgerMemberHistory>().HasIndex(x => new { x.GroupId, x.MemberId }).IsUnique();
        b.Entity<LedgerMemberHistory>().HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<LedgerMemberHistory>()
            .HasOne<GroupMember>()
            .WithMany()
            .HasForeignKey(x => x.MemberId);
        b.Entity<RequestRecord>().Property(x => x.Operation).HasMaxLength(20);
        b.Entity<RequestRecord>().Property(x => x.PayloadHash).HasMaxLength(64);
        b.Entity<RequestRecord>()
            .HasIndex(x => new
            {
                x.GroupId,
                x.UserId,
                x.Operation,
                x.ClientRequestId,
            })
            .IsUnique();
        b.Entity<RequestRecord>().HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
        b.Entity<RequestRecord>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
