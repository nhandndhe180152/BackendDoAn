using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backend.Infrastructure.DependencyInjection;

/// <summary>
/// Design-time factory dùng cho EF CLI (dotnet ef migrations add/update).
/// Dùng connection string giả — không kết nối DB thật; EF chỉ cần server version để sinh SQL.
/// </summary>
public class BackendContextDesignTimeFactory : IDesignTimeDbContextFactory<BackendContext>
{
    public BackendContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BackendContext>();

        // MySQL 8.0 — thay đổi nếu production dùng version khác
        optionsBuilder.UseMySql(
            "Server=localhost;Database=stocklite_design_time;User=root;Password=root;",
            new MySqlServerVersion(new Version(8, 0, 0)),
            builder => builder.MigrationsAssembly(typeof(BackendContext).Assembly.FullName)
        );

        return new BackendContext(optionsBuilder.Options);
    }
}
