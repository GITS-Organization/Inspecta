using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>A company: its subscription decides whether anyone in it can work, and the gate asks that one question for everyone.</summary>
public sealed class CompanyTests
{
    [Fact]
    public void ANewCompanyIsOnWithAnActiveSubscriptionAndNoReviewerYet()
    {
        var company = Company.Create("  Integrity  ");

        Assert.Equal("Integrity", company.Name);
        Assert.True(company.IsActive);
        Assert.Equal(SubscriptionStatus.Active, company.SubscriptionStatus);
        Assert.True(company.IsUsable());
        Assert.Null(company.DefaultReviewerId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ACompanyNeedsAName(string name) => Assert.Throws<ArgumentException>(() => Company.Create(name));

    [Fact]
    public void ANameTooLongOrAnEmptyIdIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Company.Create(new string('x', Company.MaxNameLength + 1)));
        Assert.Throws<ArgumentException>(() => Company.Create("Acme", id: Guid.Empty));
        Assert.Throws<ArgumentException>(() => Company.Create("Acme").SetDefaultReviewer(Guid.Empty));
    }

    [Fact]
    public void AnActiveSubscriptionPastItsEndDateCountsAsExpired()
    {
        var now = new DateTimeOffset(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
        var company = Company.Create("Acme", subscriptionEndDate: now.AddDays(10));

        Assert.True(company.HasActiveSubscription(now));
        Assert.Equal(SubscriptionStatus.Expired, company.GetEffectiveSubscriptionStatus(now.AddDays(11)));
        Assert.False(company.IsUsable(now.AddDays(11)));
    }

    [Theory]
    [InlineData(SubscriptionStatus.Suspended)]
    [InlineData(SubscriptionStatus.Expired)]
    public void ASuspendedOrExpiredSubscriptionStopsTheCompany(SubscriptionStatus status)
    {
        var company = Company.Create("Acme");

        company.UpdateSubscription(status, null);

        Assert.False(company.IsUsable());
        Assert.Equal(status, company.GetEffectiveSubscriptionStatus());
    }

    [Fact]
    public void ASwitchedOffCompanyIsNotUsableEvenWithASubscription()
    {
        var company = Company.Create("Acme");
        company.Deactivate();

        Assert.False(company.IsUsable());
        company.Activate();
        Assert.True(company.IsUsable());
    }

    [Fact]
    public async Task TheGateAnswersForOnTheCompanyAndForUsable()
    {
        var on = Company.Create("On");
        var off = Company.Create("Off");
        off.Deactivate();
        var lapsed = Company.Create("Lapsed");
        lapsed.UpdateSubscription(SubscriptionStatus.Expired, null);
        var gate = TestCompanies.GateFor(on, off, lapsed);

        Assert.True(await gate.IsActiveAsync(on.Id));
        Assert.True(await gate.IsUsableAsync(on.Id));
        Assert.False(await gate.IsActiveAsync(off.Id));
        Assert.False(await gate.IsUsableAsync(off.Id));
        Assert.True(await gate.IsActiveAsync(lapsed.Id));
        Assert.False(await gate.IsUsableAsync(lapsed.Id));
        Assert.False(await gate.IsActiveAsync(Guid.NewGuid()));
        Assert.False(await gate.IsUsableAsync(Guid.NewGuid()));
    }

    [Fact]
    public void ATechnicalManagerBelongsToACompanyAndTheOldOnesAreTheirOwn()
    {
        var company = Guid.NewGuid();
        var inside = AppUser.Create("tm@acme.com", "TM", UserRole.TechnicalManager, managerId: company);
        var legacy = AppUser.Create("old@acme.com", "Old TM", UserRole.TechnicalManager);
        var coordinator = AppUser.Create("c@acme.com", "C", UserRole.Coordinator, managerId: company);
        var owner = AppUser.Create("admin@gits.com", "Owner", UserRole.Admin);

        Assert.Equal(company, inside.CompanyId);
        Assert.Equal(legacy.Id, legacy.CompanyId);
        Assert.Equal(company, coordinator.CompanyId);
        Assert.Null(owner.CompanyId);
        Assert.Throws<ArgumentException>(() => AppUser.Create("x@gits.com", "X", UserRole.Admin, managerId: company));
    }
}
