using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Inspecta.Application.Services;

/// <summary>
/// Takes the part of an incoming email that the sender actually wrote for us: no quoted earlier messages, no
/// signature, no disclaimer. What the machine matcher reads.
/// </summary>
public static class EmailTextCleaner
{
    /// <summary>Longest text that is read; a longer email is cut (a request is never that long).</summary>
    public const int MaxLength = 60_000;

    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);
    private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private static readonly Regex Scripts = new(@"<(script|style)\b[^>]*>.*?</\1\s*>", Options | RegexOptions.Singleline, Timeout);
    private static readonly Regex QuotedBlocks = new(@"<blockquote\b[^>]*>.*?</blockquote\s*>", Options | RegexOptions.Singleline, Timeout);
    private static readonly Regex GmailQuote = new(@"<div[^>]*class=""[^""]*gmail_quote[^""]*""[^>]*>.*", Options | RegexOptions.Singleline, Timeout);
    private static readonly Regex LineBreaks = new(@"<br\s*/?>|</(p|div|tr|li|h[1-6]|table)\s*>", Options, Timeout);
    private static readonly Regex Cells = new(@"</t[dh]\s*>", Options, Timeout);
    private static readonly Regex Tags = new("<[^>]+>", Options, Timeout);

    private static readonly Regex ForwardMarker = new(
        @"^\s*(?:-{2,}\s*(?:original message|forwarded message)\b.*|begin forwarded message:?.*|-{2,}\s*الرسالة الأصلية.*)$", Options, Timeout);
    private static readonly Regex ReplyMarker = new(
        @"^\s*(?:_{5,}|on\s.{5,300}wrote:?|.{0,300}\bwrote:\s*|في\s.{5,200}(?:كتب|كتبت).*)$", Options, Timeout);
    private static readonly Regex HeaderStart = new(@"^\s*(?:from|de|von|من)\s*:", Options, Timeout);
    private static readonly Regex HeaderField = new(@"^\s*(?:sent|date|to|cc|subject|إلى|الموضوع|التاريخ)\s*:", Options, Timeout);
    private static readonly Regex Closing = new(
        @"^\s*(?:best regards|kind regards|warm regards|regards|best wishes|sincerely|thanks|thank you|many thanks|thanks and regards|with appreciation|مع تحياتي|تحياتي|مع الشكر|شكرا|شكراً)\s*[,.!]*\s*$", Options, Timeout);
    private static readonly Regex Signature = new(@"^\s*(?:--\s*|sent from my .+|get outlook for .+)$", Options, Timeout);
    private static readonly Regex Disclaimer = new(
        @"^\s*(?:this (?:e-?mail|message) (?:and any|is|may)|the information (?:contained|in this)|disclaimer|confidentiality notice|هذه الرسالة)", Options, Timeout);

    /// <summary>The subject and the new text of the email, one line per line of the original. Empty when there is nothing.</summary>
    public static string NewText(string? subject, string? textBody, string? htmlBody)
    {
        try
        {
            var body = string.IsNullOrWhiteSpace(textBody) ? HtmlToText(htmlBody) : textBody;
            var text = Clip(CutAtReply(body ?? string.Empty));
            var title = string.IsNullOrWhiteSpace(subject) ? string.Empty : subject.Trim();
            return title.Length == 0 ? text : $"{title}\n{text}";
        }
        catch (RegexMatchTimeoutException)
        {
            return string.Empty;
        }
    }

    public static string HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var text = Scripts.Replace(html, " ");
        text = QuotedBlocks.Replace(text, " ");
        text = GmailQuote.Replace(text, " ");
        text = LineBreaks.Replace(text, "\n");
        text = Cells.Replace(text, "\t");
        text = Tags.Replace(text, " ");
        return WebUtility.HtmlDecode(text);
    }

    private static string CutAtReply(string body)
    {
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var kept = new StringBuilder();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.TrimStart().StartsWith('>')) continue;

            if (ForwardMarker.IsMatch(line))
            {
                // A forwarded request has its content below the marker: keep reading, without the forwarded headers,
                // unless the sender already wrote something of their own above it.
                if (Meaningful(kept) >= 40) break;
                while (index + 1 < lines.Length && (HeaderField.IsMatch(lines[index + 1]) || HeaderStart.IsMatch(lines[index + 1]) || lines[index + 1].Trim().Length == 0))
                    index++;
                continue;
            }

            if (ReplyMarker.IsMatch(line)) break;
            if (HeaderStart.IsMatch(line) && Enumerable.Range(index + 1, Math.Min(6, lines.Length - index - 1)).Any(next => HeaderField.IsMatch(lines[next])))
            {
                if (Meaningful(kept) >= 40) break;
                continue;
            }

            if (Signature.IsMatch(line) || Closing.IsMatch(line) || Disclaimer.IsMatch(line)) break;
            kept.Append(line).Append('\n');
            if (kept.Length > MaxLength * 2) break;
        }

        return kept.ToString().Trim();
    }

    private static int Meaningful(StringBuilder text) => text.ToString().Count(char.IsLetterOrDigit);

    private static string Clip(string value) => value.Length <= MaxLength ? value : value[..MaxLength];
}
