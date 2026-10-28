using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.AspNetCore.Http;

namespace Inspecta.Infrastructure.Data;

public sealed class InspectaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<InspectaDbContext>
{
    public InspectaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InspectaDbContext>()
            .UseNpgsql("Host=localhost;Database=inspecta_design;Username=postgres;Password=postgres")
            .Options;
        return new InspectaDbContext(options, new CurrentTenant(new HttpContextAccessor()));
    }
}