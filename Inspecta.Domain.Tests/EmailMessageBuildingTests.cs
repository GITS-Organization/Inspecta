using Inspecta.Application.Interfaces;
using Inspecta.Infrastructure.Mail;

namespace Inspecta.Domain.Tests;

/// <summary>What the outgoing email looks like: an email may go without a file (a renewal reminder), and the ones with a file keep it exactly.</summary>
public sealed class EmailMessageBuildingTests
{
    private static OutgoingEmail Email(string? fileName, byte[]? content) => new(
        "hager@integrity-intl.com", "Hager Samy", "certs@deltasteel.com", "Delta Steel Co.", "Periodic inspection due",
        "Dear Delta Steel Co. team,", "<abc@integrity-intl.com>", null, null, fileName, content);

    [Fact]
    public void AnEmailWithoutAnAttachmentHasNoFilePart()
    {
        var message = MailKitSmtpEmailSender.BuildMessage(Email(null, null));

        Assert.Empty(message.Attachments);
        Assert.Equal("Dear Delta Steel Co. team,", message.TextBody);
        Assert.Equal("Periodic inspection due", message.Subject);
    }

    [Theory]
    [InlineData("quotation-QT-2026-0001.pdf", "application/pdf")]
    [InlineData("certificates.zip", "application/zip")]
    public void QuoteCertificateAndInvoiceEmailsStillCarryTheirAttachment(string fileName, string mimeType)
    {
        var message = MailKitSmtpEmailSender.BuildMessage(Email(fileName, [1, 2, 3, 4]));

        var attachment = Assert.IsType<MimeKit.MimePart>(Assert.Single(message.Attachments));
        Assert.Equal(fileName, attachment.FileName);
        Assert.Equal(mimeType, attachment.ContentType.MimeType);
        using var content = new MemoryStream();
        attachment.Content!.DecodeTo(content);
        Assert.Equal([1, 2, 3, 4], content.ToArray());
    }
}
