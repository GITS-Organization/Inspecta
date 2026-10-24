using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inspecta.Infrastructure.Data;

public sealed class InspectaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<InspectaDbContext>
{
    public InspectaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InspectaDbContext>()
            .UseSqlite("Data Source=inspecta.design.db")
            .Options;
        return new InspectaDbContext(options);
    }
}