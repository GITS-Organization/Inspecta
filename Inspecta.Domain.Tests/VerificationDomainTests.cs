using Inspecta.Application.Certificates;
using Inspecta.Domain.Entities;
using Inspecta.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Inspecta.Domain.Tests;

/// <summary>A company's own verification address (step 1): the host rules, the QR address and the migration that adds the table.</summary>
public sealed class VerificationDomainTests
{
    [Theory]
    [InlineData("verify.acme-inspection.com", "verify.acme-inspection.com")]
    [InlineData("  Verify.Acme-Inspection.COM  ", "verify.acme-inspection.com")]
    [InlineData("acme.io", "acme.io")]
    [InlineData("a.b.c.d.example.org", "a.b.c.d.example.org")]
    [InlineData("xn--bcher-kva.example", "xn--bcher-kva.example")]
    [InlineData("certs2.acme.co.uk", "certs2.acme.co.uk")]
    public void AValidHostIsAcceptedAndStoredInLowerCase(string input, string expected)
    {
        var check = VerificationHost.Check(input);

        Assert.True(check.IsValid);
        Assert.Equal(expected, check.Host);
        Assert.Null(check.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://verify.acme.com")]
    [InlineData("http://verify.acme.com")]
    [InlineData("verify.acme.com/verify")]
    [InlineData("verify.acme.com/")]
    [InlineData("verify.acme.com?x=1")]
    [InlineData("verify.acme.com#top")]
    [InlineData("verify.acme.com:8443")]
    [InlineData("user@verify.acme.com")]
    [InlineData("verify_certs.acme.com")]
    [InlineData("verify acme.com")]
    [InlineData("verify.acme.com.")]
    [InlineData("localhost")]
    [InlineData("acme")]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.20")]
    [InlineData("[::1]")]
    [InlineData("2001:db8::1")]
    [InlineData("1.2.3.4.5")]
    [InlineData("-verify.acme.com")]
    [InlineData("verify-.acme.com")]
    [InlineData("verify..acme.com")]
    [InlineData(".acme.com")]
    [InlineData("*.acme.com")]
    [InlineData("bücher.example")]
    public void AnythingThatIsNotAPlainDnsNameIsRefused(string? input)
    {
        var check = VerificationHost.Check(input);

        Assert.False(check.IsValid);
        Assert.Null(check.Host);
        Assert.Equal("invalid_host", check.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(check.Message));
    }

    [Fact]
    public void ALabelOverSixtyThreeCharactersOrAHostOver253IsRefused()
    {
        Assert.Equal("invalid_host", VerificationHost.Check(new string('a', 64) + ".acme.com").ErrorCode);
        Assert.True(VerificationHost.Check(new string('a', 63) + ".acme.com").IsValid);

        var tooLong = string.Join('.', Enumerable.Repeat(new string('a', 50), 5)) + ".com";
        Assert.True(tooLong.Length > 253);
        Assert.Equal("invalid_host", VerificationHost.Check(tooLong).ErrorCode);
        var longest = string.Join('.', Enumerable.Repeat(new string('a', 50), 4).Concat(["a".PadRight(40, 'b'), "com"]));
        Assert.True(longest.Length <= 253);
        Assert.True(VerificationHost.Check(longest).IsValid);
    }

    [Theory]
    [InlineData("api.gitsplatform.com")]
    [InlineData("gitsplatform.com")]
    [InlineData("verify.gitsplatform.com")]
    [InlineData("A.B.GitsPlatform.com")]
    public void AnAddressOfTheServiceItselfIsReserved(string input)
    {
        var check = VerificationHost.Check(input);

        Assert.False(check.IsValid);
        Assert.Equal("host_reserved", check.ErrorCode);
    }

    [Fact]
    public void ThePublicAddressInTheConfigurationIsReservedToo()
    {
        Assert.Equal("host_reserved", VerificationHost.Check("certs.inspecta.example", ownHost: "certs.inspecta.example").ErrorCode);
        Assert.True(VerificationHost.Check("certs.acme.example", ownHost: "certs.inspecta.example").IsValid);
        Assert.Equal("certs.inspecta.example", VerificationHost.HostOf("https://certs.inspecta.example/"));
        Assert.Null(VerificationHost.HostOf("not a url"));
    }

    [Fact]
    public void AHostThatLooksLikeTheServiceButIsNotItsSubdomainIsAllowed()
    {
        Assert.True(VerificationHost.Check("notgitsplatform.com").IsValid);
        Assert.True(VerificationHost.Check("gitsplatform.com.acme.example").IsValid);
    }

    [Fact]
    public void TheQrAddressOfACompanyHostIsHttpsAndTheOldOneIsUnchanged()
    {
        Assert.Equal("https://verify.acme.com/verify/ABC123", CertificateOptions.VerifyUrlOn("verify.acme.com", "ABC123"));
        Assert.Equal("https://api.gitsplatform.com/verify/ABC123", new CertificateOptions().VerifyUrl("ABC123"));
        Assert.Equal("https://x.example/verify/ABC123", new CertificateOptions("https://x.example/").VerifyUrl("ABC123"));
    }

    [Fact]
    public void ANewDomainIsLowerCaseWithItsOwnIdAndAClock()
    {
        var company = Guid.NewGuid();
        var domain = CompanyVerificationDomain.Create(company, "Verify.Acme.COM", isPrimary: true);

        Assert.Equal(company, domain.ManagerId);
        Assert.Equal("verify.acme.com", domain.Host);
        Assert.True(domain.IsPrimary);
        Assert.NotEqual(Guid.Empty, domain.Id);
        Assert.True(domain.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        domain.Demote();
        Assert.False(domain.IsPrimary);
        domain.MakePrimary();
        Assert.True(domain.IsPrimary);
    }

    // ---- The migration is additive ----------------------------------------------------------------------------------

    private static Migration TheMigration() =>
        (Migration)Activator.CreateInstance(typeof(InspectaDbContext).Assembly.GetTypes()
            .Single(type => type.Name == "AddCompanyVerificationDomains" && type.IsSubclassOf(typeof(Migration))))!;

    [Fact]
    public void TheMigrationOnlyCreatesTheNewTableAndItsIndexesAndSeedsNothing()
    {
        var up = TheMigration().UpOperations;

        var table = Assert.Single(up.OfType<CreateTableOperation>());
        Assert.Equal("CompanyVerificationDomains", table.Name);
        Assert.Equal(["CreatedAt", "Host", "Id", "IsPrimary", "ManagerId"], table.Columns.Select(column => column.Name).Order().ToArray());
        Assert.All(up.Where(operation => operation is not CreateTableOperation), operation =>
        {
            var index = Assert.IsType<CreateIndexOperation>(operation);
            Assert.Equal("CompanyVerificationDomains", index.Table);
        });
        Assert.Contains(up.OfType<CreateIndexOperation>(), index => index.IsUnique && index.Columns.SequenceEqual(["Host"]));
        Assert.DoesNotContain(up, operation => operation is InsertDataOperation or UpdateDataOperation or DeleteDataOperation or SqlOperation or
            DropTableOperation or DropColumnOperation or AlterColumnOperation or RenameTableOperation or RenameColumnOperation or AddColumnOperation);
    }

    [Fact]
    public void TheMigrationUndoesOnlyItsOwnTable()
    {
        var down = TheMigration().DownOperations;

        Assert.Equal("CompanyVerificationDomains", Assert.Single(down.OfType<DropTableOperation>()).Name);
        Assert.Single(down);
    }
}
