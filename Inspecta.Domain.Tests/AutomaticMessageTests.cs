using Inspecta.Infrastructure.Mail;
using MimeKit;

namespace Inspecta.Domain.Tests;

/// <summary>The messages that are never the client's answer: out-of-office replies, bounces and bulk mail.</summary>
public sealed class AutomaticMessageTests
{
    private static MimeMessage Message(string from = "buyer@alnoor.com", params (string Name, string Value)[] headers)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Sender", from));
        message.To.Add(new MailboxAddress("Coordinator", "coordinator@acme.com"));
        message.Subject = "Re: Quotation";
        message.Body = new TextPart("plain") { Text = "Hello" };
        foreach (var (name, value) in headers) message.Headers.Add(name, value);
        return message;
    }

    [Fact]
    public void AnOrdinaryMessageIsNotAutomatic()
    {
        Assert.False(AutomaticMessageDetector.IsAutomatic(Message()));
        Assert.False(AutomaticMessageDetector.IsAutomatic(Message(headers: ("Auto-Submitted", "no"))));
    }

    [Theory]
    [InlineData("Auto-Submitted", "auto-replied")]
    [InlineData("Auto-Submitted", "auto-generated")]
    [InlineData("Precedence", "bulk")]
    [InlineData("Precedence", "auto_reply")]
    [InlineData("X-Autoreply", "yes")]
    [InlineData("X-Autorespond", "yes")]
    [InlineData("X-Auto-Response-Suppress", "DR, OOF, AutoReply")]
    [InlineData("Return-Path", "<>")]
    public void OutOfOfficeAndBulkHeadersMarkAMessageAutomatic(string header, string value) =>
        Assert.True(AutomaticMessageDetector.IsAutomatic(Message(headers: (header, value))));

    [Theory]
    [InlineData("MAILER-DAEMON@mail.alnoor.com")]
    [InlineData("postmaster@mail.alnoor.com")]
    public void ABounceFromTheMailSystemIsAutomatic(string sender) =>
        Assert.True(AutomaticMessageDetector.IsAutomatic(Message(sender)));

    [Fact]
    public void ADeliveryReportIsAutomatic()
    {
        var message = Message();
        message.Body = new Multipart("report") { new TextPart("plain") { Text = "Undeliverable" } };

        Assert.True(AutomaticMessageDetector.IsAutomatic(message));
    }
}
