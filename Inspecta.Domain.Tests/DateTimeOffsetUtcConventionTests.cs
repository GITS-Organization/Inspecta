using Inspecta.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Inspecta.Domain.Tests;

/// <summary>
/// PostgreSQL ('timestamp with time zone') only accepts a DateTimeOffset with offset 0. An email dated +02:00 once made the import fail
/// and the email was lost. Every DateTimeOffset of the model is converted to UTC on write, so a new entity cannot bring the bug back.
/// </summary>
public sealed class DateTimeOffsetUtcConventionTests
{
    private static InspectaDbContext Postgres() => new(
        new DbContextOptionsBuilder<InspectaDbContext>()
            .UseNpgsql("Host=localhost;Database=inspecta_test;Username=test;Password=test")
            .Options,
        new CurrentTenant(new HttpContextAccessor()));

    [Fact]
    public void EveryDateTimeOffsetPropertyOfTheModelIsConvertedToUtcOnWrite()
    {
        using var db = Postgres();
        var properties = db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
            .ToList();
        Assert.NotEmpty(properties);

        var plusFiveThirty = new DateTimeOffset(2026, 11, 21, 9, 15, 0, TimeSpan.FromMinutes(330));
        foreach (var property in properties)
        {
            var converter = property.GetValueConverter();
            Assert.True(converter is not null, $"{property.DeclaringType.DisplayName()}.{property.Name} has no UTC conversion.");
            var written = Assert.IsType<DateTimeOffset>(converter!.ConvertToProvider(plusFiveThirty));
            Assert.Equal(TimeSpan.Zero, written.Offset);
            Assert.Equal(plusFiveThirty, written);
        }
    }
}
