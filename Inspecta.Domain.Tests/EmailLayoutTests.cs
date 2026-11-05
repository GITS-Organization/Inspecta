using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>The look of the emails: the text, the signature with the logo, and the email being answered.</summary>
public sealed class EmailLayoutTests
{
    private static readonly EmailBranding Branding = new(
        "Integrity International for Inspection", "21 Nakib Sharif Ramzy st, Smouha, Alexandria, Egypt", "www.integrity-intl.com", [1, 2, 3]);

    private sealed class FixedBranding(EmailBranding branding) : IEmailBranding
    {
        public EmailBranding Get() => branding;
    }

    private static EmailSignatureInfo Signature(string name = "Hager Samy") =>
        new(name, "Business Coordinator", "+20 1035945426", "hager.samy@integrity-intl.com", Branding);

    [Theory]
    [InlineData("<script ")]
    [InlineData("<")]
    [InlineData("<p ")]
    public void AnEmailMadeToChokeTheHtmlCleanupIsAnsweredQuicklyNotAfterMinutesOfWork(string piece)
    {
        // A sender controls this text: thousands of unclosed tags must not turn quoting their email into minutes of CPU.
        var hostile = string.Concat(Enumerable.Repeat(piece, 150_000 / piece.Length));
        var source = RequestEmail.Create(
            Guid.NewGuid(), Guid.NewGuid(), "1", 1, null, hostile, "client@example.com", "Client", "Inspection",
            null, null, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);

        var started = DateTime.UtcNow;
        var quoted = EmailLayout.Quote(source);

        Assert.NotNull(quoted);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(2), $"took {DateTime.UtcNow - started}");
    }

    [Fact]
    public void TheSignatureHasTheNameTitleContactsAddressWebsiteAndTheLogo()
    {
        var email = EmailLayout.Build("Dears,\ngood day,\n\nRegards,", Signature(), original: null);

        Assert.StartsWith("Dears,", email.Text);
        foreach (var expected in new[]
                 {
                     "With Appreciation,", "H.Samy", "Hager Samy", "Business Coordinator", "+20 1035945426", "hager.samy@integrity-intl.com",
                     "21 Nakib Sharif Ramzy st, Smouha, Alexandria, Egypt", "https://www.integrity-intl.com"
                 })
        {
            Assert.Contains(expected, email.Text);
            Assert.Contains(expected, email.Html);
        }

        Assert.Contains("src=\"cid:inspecta-logo\"", email.Html);
        var logo = Assert.Single(email.InlineImages);
        Assert.Equal(("inspecta-logo", "image/png"), (logo.FileName, logo.ContentType));
        Assert.Equal(new byte[] { 1, 2, 3 }, logo.Content);
    }

    [Fact]
    public void APreviewPointsTheLogoAtTheWebAddressInsteadOfCarryingIt()
    {
        var email = EmailLayout.Build("Text", Signature(), null, EmailLayout.PreviewLogoPath);

        Assert.Empty(email.InlineImages);
        Assert.Contains($"src=\"{EmailLayout.PreviewLogoPath}\"", email.Html);
    }

    [Fact]
    public void TheEmailBeingAnsweredIsQuotedBelowTheSignature()
    {
        var original = new QuotedEmail(
            "Buyer <buyer@alnoor.com>", new DateTimeOffset(2026, 10, 27, 13, 33, 0, TimeSpan.Zero), "Hager <hager@integrity-intl.com>", "Rewan <rewan@integrity-intl.com>",
            "Certificates Format", "Please send the format.");

        var email = EmailLayout.Build("Regards,", Signature(), original);

        Assert.True(email.Text.IndexOf("With Appreciation,", StringComparison.Ordinal) < email.Text.IndexOf("From: Buyer <buyer@alnoor.com>", StringComparison.Ordinal));
        Assert.Contains("Subject: Certificates Format", email.Text);
        Assert.Contains("Cc: Rewan <rewan@integrity-intl.com>", email.Text);
        Assert.Contains("Tuesday, 27 October 2026 at", email.Text);
        Assert.Contains("Please send the format.", email.Text);
        Assert.Contains("<b>Subject:</b> Certificates Format", email.Html);
        Assert.Contains("Buyer &lt;buyer@alnoor.com&gt;", email.Html);
    }

    [Fact]
    public void ALongOriginalIsShortenedAndNothingIsQuotedWhenThereIsNoOriginal()
    {
        var long8000 = new string('x', 8000);
        var email = EmailLayout.Build("Regards,", Signature(), new QuotedEmail("a@b.com", null, null, null, "S", long8000));

        Assert.InRange(email.Text.Length, 3000, 4000);
        Assert.EndsWith("..." + Environment.NewLine, email.Text);
        Assert.DoesNotContain("From:", EmailLayout.Build("Regards,", Signature(), null).Text);
    }

    [Fact]
    public void TextFromAPersonCannotInjectHtml()
    {
        var email = EmailLayout.Build("<script>alert(1)</script>", Signature("<img src=x onerror=alert(1)>"), new QuotedEmail("<b>x</b>", null, null, null, "<i>s</i>", "<u>t</u>"));

        Assert.DoesNotContain("<script>", email.Html);
        Assert.DoesNotContain("<img src=x", email.Html);
        Assert.DoesNotContain("<u>t</u>", email.Html);
        Assert.Contains("&lt;script&gt;", email.Html);
    }

    [Theory]
    [InlineData("Hager Samy", "H.Samy")]
    [InlineData("Eng. Hager Samy", "H.Samy")]
    [InlineData("Ahmed", "Ahmed")]
    public void TheShortNameIsTheInitialAndTheLastName(string fullName, string expected) =>
        Assert.Equal(expected, EmailLayout.ShortName(fullName));

    [Fact]
    public void ACoordinatorSignsWithTheTitleOfTheirRoleAndWithoutBrandingNothingIsAdded()
    {
        var coordinator = AppUser.Create("hager@integrity-intl.com", "Hager Samy", UserRole.Coordinator);

        var signed = EmailLayout.ForCoordinator("Regards,", coordinator, "hager@integrity-intl.com", null, new FixedBranding(Branding));
        var plain = EmailLayout.ForCoordinator("Regards,", coordinator, "hager@integrity-intl.com", null, null);

        Assert.Contains("Business Coordinator", signed.Text);
        Assert.Equal("Regards,", plain.Text);
        Assert.Empty(plain.InlineImages);
        Assert.Equal("Technical Manager", EmailLayout.JobTitleOf(UserRole.TechnicalManager));
    }
}
