using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SplitTogether.Data;

public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDb>
{
    public AppDb CreateDbContext(string[] args)
    {
        var connection =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=splittogether;Username=split_migrator";
        return new(
            new DbContextOptionsBuilder<AppDb>()
                .UseNpgsql(connection, pg => pg.SetPostgresVersion(18, 0))
                .Options
        );
    }
}
