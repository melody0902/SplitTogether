using Microsoft.EntityFrameworkCore;
using SplitTogether.Data;
using SplitTogether.TestSupport;

var connection = TestDatabase.Validate(Environment.GetEnvironmentVariable("SPLIT_TEST_CONNECTION"));
Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
var app = AppBootstrap.Build(
    args,
    b =>
    {
        b.Configuration["ConnectionStrings:Default"] = connection;
        b.Services.AddSingleton<IStartupFilter, TestLoginStartupFilter>();
    }
);
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.MigrateAsync();
}
app.Run();
