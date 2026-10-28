using System.Net;
using System.Text.RegularExpressions;
using Inspecta.Application.Contracts.Email;
using Inspecta.Application.Interfaces;

namespace Inspecta.Application.Services;

public sealed class EmailParserService : IEmailParserService
{
    private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex ClientLabel = new(
        @"(?im)^\s*(?:client\s+name|client|name)\s*:\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ClientPhrase = new(
        @"(?im)\b(?:my name is|this is)\s+(?<value>[^,.;!?\r\n]+?)(?:\s+(?:and|but)\b|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LocationLabel = new(
        @"(?im)^\s*(?:location|site|city)\s*:\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LocationPhrase = new(
        @"(?im)\b(?:located|based|site is)\s+(?:in|at)\s+(?<value>[^.!?\r\n]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MachineDetailsLabel = new(
        @"(?im)^\s*(?:machine details|machine|equipment)\s*:\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ModelLabel = new(
        @"(?im)^\s*(?:machine\s+)?model\s*:\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SerialLabel = new(
        @"(?im)^\s*serial(?:\s*(?:number|no\.?))?\s*:\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex IssueLabel = new(
        @"(?im)^\s*(?:issue|problem|issue description)\s*:\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ParsedIncomingEmail Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new ParsedIncomingEmail(null, null, null);

        var text = WebUtility.HtmlDecode(HtmlTag.Replace(body, " "))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var client = FirstValue(ClientLabel, text) ?? FirstValue(ClientPhrase, text);
        var location = FirstValue(LocationLabel, text) ?? FirstValue(LocationPhrase, text);
        var machineDetails = FirstValue(MachineDetailsLabel, text);

        if (machineDetails is null)
        {
            var parts = new[]
            {
                FirstValue(ModelLabel, text) is { } model ? $"Model: {model}" : null,
                FirstValue(SerialLabel, text) is { } serial ? $"Serial: {serial}" : null,
                FirstValue(IssueLabel, text) is { } issue ? $"Issue: {issue}" : null
            };
            machineDetails = string.Join(Environment.NewLine, parts.Where(value => value is not null));
            if (machineDetails.Length == 0)
                machineDetails = null;
        }

        return new ParsedIncomingEmail(
            Clip(client, 200),
            Clip(location, 500),
            Clip(machineDetails, 4000));
    }

    private static string? FirstValue(Regex pattern, string text)
    {
        var match = pattern.Match(text);
        return match.Success ? Normalize(match.Groups["value"].Value) : null;
    }

    private static string? Normalize(string value)
    {
        var normalized = Regex.Replace(value, @"\s+", " ").Trim(' ', '\t', '-', ',', ';');
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? Clip(string? value, int maxLength) =>
        value is { Length: > 0 } ? value[..Math.Min(value.Length, maxLength)] : null;
}
