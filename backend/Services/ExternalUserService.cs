using Microsoft.EntityFrameworkCore;
using SplitTogether.Data;
using SplitTogether.Models;

namespace SplitTogether.Services;

public class ExternalUserService(AppDb db)
{
    public async Task<User> Resolve(
        string provider,
        string providerId,
        string name,
        string? email,
        string? picture,
        CancellationToken ct
    )
    {
        // READ COMMITTED + lock followed by a separate SELECT sees the winner's commit.
        // Row locking alone would not protect a first login, because no row exists yet.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await DatabaseLocks.Acquire(db, $"identity:{provider}:{providerId}", ct);
        var login = await db.ExternalLogins.SingleOrDefaultAsync(
            x => x.Provider == provider && x.ProviderUserId == providerId,
            ct
        );
        User user;
        if (login is null)
        {
            user = new User();
            db.Users.Add(user);
            db.ExternalLogins.Add(
                new()
                {
                    UserId = user.Id,
                    Provider = provider,
                    ProviderUserId = providerId,
                }
            );
        }
        else
            user = await db.Users.SingleAsync(x => x.Id == login.UserId, ct);
        user.Name = name[..Math.Min(name.Length, 200)];
        user.Email = email;
        user.Picture = picture;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return user;
    }
}
