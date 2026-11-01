using System.Text;
using System.Text.RegularExpressions;

namespace Inspecta.Application.Services;

/// <summary>A machine of a client that an email can be matched against.</summary>
public sealed record MachineCandidate(
    Guid EquipmentId,
    string Name,
    string? Category,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? AssetTag,
    string? Capacity,
    string? Location);

public enum MachineMatchReason
{
    Serial = 1,
    Name = 2,
    Model = 3
}

/// <summary>
/// A machine of the client that the email talks about. <c>Confidence</c> is 0 to 1; from
/// <see cref="AutoFillThreshold"/> up, and not <c>Ambiguous</c>, the machine is put on the request by itself.
/// </summary>
public sealed record MachineMatch(
    MachineCandidate Machine,
    int Quantity,
    double Confidence,
    MachineMatchReason Reason,
    string Evidence,
    bool Ambiguous)
{
    public const double AutoFillThreshold = 0.85;
    public const double SuggestThreshold = 0.6;

    public bool IsConfident => !Ambiguous && Confidence >= AutoFillThreshold;
}

/// <summary>
/// Finds the machines of one client in the text of an email. Everything is plain text work (no outside service): the
/// text is normalised (case, Arabic letter forms and digits), then a machine is recognised by its serial number or asset
/// tag, by its name (all its words close together, plurals and one-letter typos tolerated) or by its model, and its
/// quantity is read from the words around the name. Precision comes first: a machine that could be any of several
/// similar ones is only suggested, never put on the request by itself.
/// </summary>
public static class MachineMatcher
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);
    private static readonly Regex ListMarker = new(@"^\s*(?:\(?\d{1,2}[.)\-:]\s+|[-*•●▪◦]\s+)", RegexOptions.Compiled | RegexOptions.CultureInvariant, Timeout);

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "of", "and", "for", "with", "a", "an", "to", "in", "on", "و", "من", "في", "علي", "على", "ال"
    };

    // Words that say nothing about which machine it is.
    private static readonly HashSet<string> GenericWords = new(StringComparer.Ordinal)
    {
        "equipment", "equiment", "machine", "item", "unit", "device", "معده", "ماكينه", "جهاز"
    };

    private static readonly HashSet<string> NotAQuantity = new(StringComparer.Ordinal)
    {
        "item", "no", "pos", "position", "ref", "رقم", "بند", "page", "po", "pr", "qt"
    };

    private static readonly HashSet<string> QuantityAfterNumber = new(StringComparer.Ordinal)
    {
        "nos", "no", "pcs", "pc", "unit", "units", "set", "sets", "عدد", "وحده", "وحدات", "قطعه", "قطع"
    };

    private static readonly HashSet<string> QuantityBeforeNumber = new(StringComparer.Ordinal)
    {
        "x", "qty", "quantity", "عدد", "كميه"
    };

    public static IReadOnlyList<MachineMatch> Match(string? text, IReadOnlyList<MachineCandidate> machines)
    {
        if (string.IsNullOrWhiteSpace(text) || machines.Count == 0) return [];

        try
        {
            var document = Document.Parse(text);
            if (document.Tokens.Count == 0) return [];

            var found = new List<Hit>();
            foreach (var machine in machines)
            {
                var hit = Evaluate(document, machine);
                if (hit is not null && hit.Confidence >= MachineMatch.SuggestThreshold) found.Add(hit);
            }

            return Resolve(found, document);
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // One machine against the text

    private sealed record Hit(MachineCandidate Machine, double Confidence, MachineMatchReason Reason, int Start, int End, string GroupKey);

    private static Hit? Evaluate(Document document, MachineCandidate machine)
    {
        var nameTokens = NameTokens(machine.Name);
        var groupKey = string.Join(' ', nameTokens.OrderBy(token => token, StringComparer.Ordinal));

        var serial = Compact(machine.SerialNumber);
        if (IsIdentifier(serial) && document.ContainsIdentifier(serial))
            return Located(document, machine, 1.0, MachineMatchReason.Serial, serial, groupKey);
        var tag = Compact(machine.AssetTag);
        if (IsIdentifier(tag) && document.ContainsIdentifier(tag))
            return Located(document, machine, 0.97, MachineMatchReason.Serial, tag, groupKey);

        var name = MatchName(document, nameTokens);
        var model = Compact(machine.Model);
        var modelFound = IsIdentifier(model) && document.ContainsIdentifier(model);

        if (name is null)
        {
            return modelFound ? Located(document, machine, 0.85, MachineMatchReason.Model, model, groupKey) : null;
        }

        var confidence = name.Score;
        if (modelFound) confidence += 0.1;
        if (NearbyHas(document, Tokens(machine.Capacity), name.Start, name.End)) confidence += 0.07;
        if (NearbyHas(document, Tokens(machine.Manufacturer), name.Start, name.End)) confidence += 0.03;
        return new Hit(machine, Math.Min(0.98, confidence), MachineMatchReason.Name, name.Start, name.End, groupKey);
    }

    private static Hit Located(Document document, MachineCandidate machine, double confidence, MachineMatchReason reason, string identifier, string groupKey)
    {
        var position = document.PositionOfIdentifier(identifier);
        return new Hit(machine, confidence, reason, position, position, groupKey);
    }

    private static bool IsIdentifier(string compact) =>
        compact.Length >= 4 && compact.Any(char.IsDigit);

    private sealed record NameMatch(double Score, int Start, int End);

    private static NameMatch? MatchName(Document document, IReadOnlyList<string> nameTokens)
    {
        var count = nameTokens.Count;
        if (count == 0) return null;

        // Where each word of the name is in the text: exactly, or with a one or two letter typo.
        var places = new List<(int Position, bool Fuzzy)>[count];
        for (var index = 0; index < count; index++)
            places[index] = document.PlacesOf(nameTokens[index]);
        if (places.All(list => list.Count == 0)) return null;

        var rarest = Enumerable.Range(0, count).Where(index => places[index].Count > 0).MinBy(index => places[index].Count);
        var radius = count + 3;
        NameMatch? best = null;
        foreach (var anchor in places[rarest].Select(place => place.Position).Distinct())
        {
            var matched = 0;
            var fuzzy = 0;
            var first = int.MaxValue;
            var last = int.MinValue;
            for (var index = 0; index < count; index++)
            {
                var near = places[index]
                    .Where(place => Math.Abs(place.Position - anchor) <= radius && document.SameLine(place.Position, anchor, radius))
                    .OrderBy(place => place.Fuzzy)
                    .ThenBy(place => Math.Abs(place.Position - anchor))
                    .Cast<(int Position, bool Fuzzy)?>()
                    .FirstOrDefault();
                if (near is null) continue;
                matched++;
                if (near.Value.Fuzzy) fuzzy++;
                first = Math.Min(first, near.Value.Position);
                last = Math.Max(last, near.Value.Position);
            }

            double score;
            if (matched == count)
            {
                var span = last - first + 1;
                score = fuzzy > 0 ? 0.78 : span <= count + 1 ? 0.92 : 0.82;
                // One word is too little to be sure ("pump" is in many emails): it needs help from the model or capacity.
                if (count == 1) score = Math.Min(score, 0.78);
            }
            else if (matched >= 2 && matched * 3 >= count * 2)
            {
                score = 0.6;
            }
            else
            {
                continue;
            }

            if (best is null || score > best.Score || score == best.Score && last - first < best.End - best.Start)
                best = new NameMatch(score, first, last);
        }

        return best;
    }

    private static bool NearbyHas(Document document, IReadOnlyList<string> tokens, int start, int end)
    {
        if (tokens.Count == 0) return false;
        var from = Math.Max(0, start - 8);
        var to = Math.Min(document.Tokens.Count - 1, end + 8);
        var window = new HashSet<string>(document.Tokens.Skip(from).Take(to - from + 1), StringComparer.Ordinal);
        return tokens.All(window.Contains);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Several machines with the same name: only the clearly better one is kept, otherwise all are marked ambiguous

    private static IReadOnlyList<MachineMatch> Resolve(List<Hit> found, Document document)
    {
        var result = new List<MachineMatch>();
        foreach (var group in found.GroupBy(hit => hit.GroupKey))
        {
            var hits = group.OrderByDescending(hit => hit.Confidence).ToList();
            var byName = hits.Where(hit => hit.Reason == MachineMatchReason.Name).ToList();
            var ambiguous = new HashSet<Hit>();
            if (byName.Count > 1)
            {
                if (byName[0].Confidence - byName[1].Confidence >= 0.05)
                    hits.RemoveAll(hit => hit.Reason == MachineMatchReason.Name && hit != byName[0]);
                else
                    foreach (var hit in byName) ambiguous.Add(hit);
            }

            foreach (var hit in hits)
            {
                var isAmbiguous = ambiguous.Contains(hit);
                result.Add(new MachineMatch(
                    hit.Machine,
                    hit.Reason == MachineMatchReason.Serial ? 1 : Quantity(document, hit.Start, hit.End),
                    isAmbiguous ? Math.Min(hit.Confidence, 0.7) : hit.Confidence,
                    hit.Reason,
                    document.LineAt(hit.Start),
                    isAmbiguous));
            }
        }

        return result.OrderByDescending(match => match.Confidence).ThenBy(match => match.Machine.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // ---------------------------------------------------------------------------------------------------------
    // Quantity: "4 air receivers", "air receiver x4", "air receiver (4 nos)", "qty: 4", "عدد 4"

    private static int Quantity(Document document, int start, int end)
    {
        var tokens = document.Tokens;
        var line = document.LineOf(start);

        if (start >= 1 && document.LineOf(start - 1) == line)
        {
            var before = tokens[start - 1];
            if (IsCount(before) && !(start >= 2 && document.LineOf(start - 2) == line && NotAQuantity.Contains(tokens[start - 2])))
                return int.Parse(before, System.Globalization.CultureInfo.InvariantCulture);
            if (before == "x" && start >= 2 && document.LineOf(start - 2) == line && IsCount(tokens[start - 2]))
                return int.Parse(tokens[start - 2], System.Globalization.CultureInfo.InvariantCulture);
        }

        for (var index = end + 1; index <= Math.Min(tokens.Count - 1, end + 4) && document.LineOf(index) == line; index++)
        {
            var token = tokens[index];
            if (token.Length > 1 && token[0] == 'x' && IsCount(token[1..]))
                return int.Parse(token[1..], System.Globalization.CultureInfo.InvariantCulture);
            if (QuantityBeforeNumber.Contains(token) && index + 1 < tokens.Count && document.LineOf(index + 1) == line && IsCount(tokens[index + 1]))
                return int.Parse(tokens[index + 1], System.Globalization.CultureInfo.InvariantCulture);
            if (IsCount(token) && index + 1 < tokens.Count && document.LineOf(index + 1) == line && QuantityAfterNumber.Contains(tokens[index + 1]))
                return int.Parse(token, System.Globalization.CultureInfo.InvariantCulture);
        }

        return 1;
    }

    private static bool IsCount(string token) =>
        token.Length is >= 1 and <= 3 && token.All(char.IsAsciiDigit) && int.Parse(token, System.Globalization.CultureInfo.InvariantCulture) is >= 1 and <= 999;

    // ---------------------------------------------------------------------------------------------------------
    // Words

    private static List<string> NameTokens(string name) =>
        Tokens(name).Where(token => !GenericWords.Contains(token)).Distinct().ToList();

    private static List<string> Tokens(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : Tokenize(Normalize(value)).Select(Stem).Where(token => token.Length > 0 && !StopWords.Contains(token)).ToList();

    private static string Compact(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : new string(Normalize(value).Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Lower case; Arabic letter forms, digits and marks made uniform so the same word is written one way.</summary>
    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var original in value.Normalize(NormalizationForm.FormKC))
        {
            var ch = original;
            if (ch is (>= 'ً' and <= 'ٟ') or 'ٰ' or 'ـ') continue;
            if (ch is >= '٠' and <= '٩') ch = (char)('0' + (ch - '٠'));
            else if (ch is >= '۰' and <= '۹') ch = (char)('0' + (ch - '۰'));
            else if (ch is 'أ' or 'إ' or 'آ' or 'ٱ') ch = 'ا';
            else if (ch == 'ى') ch = 'ي';
            else if (ch == 'ة') ch = 'ه';
            else if (ch is '×' or '✕' or '✖')
            {
                builder.Append(" x ");
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private static IEnumerable<string> Tokenize(string normalized)
    {
        var builder = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                continue;
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
                builder.Clear();
            }
        }

        if (builder.Length > 0) yield return builder.ToString();
    }

    /// <summary>Plural and the Arabic "al" removed, so "receivers" and "receiver" are the same word.</summary>
    private static string Stem(string token)
    {
        if (token.Length > 4 && token.StartsWith("ال", StringComparison.Ordinal)) token = token[2..];
        if (token.Length <= 3 || !token.All(char.IsAsciiLetterOrDigit)) return token;

        if (token.EndsWith("ies", StringComparison.Ordinal) && token.Length > 4) return token[..^3] + "y";
        if (token.EndsWith("sses", StringComparison.Ordinal) || token.EndsWith("ches", StringComparison.Ordinal) ||
            token.EndsWith("shes", StringComparison.Ordinal) || token.EndsWith("xes", StringComparison.Ordinal))
            return token[..^2];
        if (token.EndsWith('s') && !token.EndsWith("ss", StringComparison.Ordinal) && !token.EndsWith("us", StringComparison.Ordinal) &&
            !token.EndsWith("is", StringComparison.Ordinal))
            return token[..^1];
        return token;
    }

    // ---------------------------------------------------------------------------------------------------------
    // The text, as words with their line and an index

    private sealed class Document
    {
        private readonly Dictionary<string, List<int>> index = new(StringComparer.Ordinal);
        private readonly List<int> lineOfToken = [];
        private readonly List<string> lines = [];
        private readonly Dictionary<string, int> rawTokens = new(StringComparer.Ordinal);
        private string compact = string.Empty;

        public List<string> Tokens { get; } = [];

        public static Document Parse(string text)
        {
            var document = new Document();
            var all = new StringBuilder();
            foreach (var original in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                var line = ListMarker.Replace(original, string.Empty, 1);
                var lineNumber = document.lines.Count;
                document.lines.Add(original.Trim());
                var normalized = Normalize(line);
                foreach (var raw in Tokenize(normalized))
                {
                    document.rawTokens.TryAdd(raw, document.Tokens.Count);
                    var token = Stem(raw);
                    if (StopWords.Contains(token)) continue;
                    if (!document.index.TryGetValue(token, out var positions))
                        document.index[token] = positions = [];
                    positions.Add(document.Tokens.Count);
                    document.Tokens.Add(token);
                    document.lineOfToken.Add(lineNumber);
                }

                all.Append(new string(normalized.Where(char.IsLetterOrDigit).ToArray())).Append(' ');
            }

            document.compact = all.ToString();
            return document;
        }

        public int LineOf(int position) => lineOfToken[position];

        /// <summary>Whether two positions are in the same line, or close enough that a wrapped line may join them.</summary>
        public bool SameLine(int left, int right, int radius) => Math.Abs(lineOfToken[left] - lineOfToken[right]) <= 1 && Math.Abs(left - right) <= radius;

        public string LineAt(int position)
        {
            var line = lines[lineOfToken[Math.Clamp(position, 0, lineOfToken.Count - 1)]];
            return line.Length <= 160 ? line : line[..160] + "...";
        }

        public List<(int Position, bool Fuzzy)> PlacesOf(string token)
        {
            var places = new List<(int, bool)>();
            if (index.TryGetValue(token, out var exact)) places.AddRange(exact.Select(position => (position, false)));
            if (token.Length < 5) return places;

            var allowed = token.Length >= 9 ? 2 : 1;
            foreach (var (word, positions) in index)
            {
                if (word == token || word.Length < 5 || word[0] != token[0] || Math.Abs(word.Length - token.Length) > allowed) continue;
                if (Distance(word, token, allowed) <= allowed) places.AddRange(positions.Select(position => (position, true)));
            }

            return places;
        }

        public bool ContainsIdentifier(string identifier) =>
            rawTokens.ContainsKey(identifier) || identifier.Length >= 6 && compact.Contains(identifier, StringComparison.Ordinal);

        public int PositionOfIdentifier(string identifier)
        {
            if (rawTokens.TryGetValue(identifier, out var exact)) return Math.Min(exact, Math.Max(0, Tokens.Count - 1));
            for (var position = 0; position < Tokens.Count; position++)
                if (Tokens[position].Length >= 3 && identifier.Contains(Tokens[position], StringComparison.Ordinal))
                    return position;
            return 0;
        }

        private static int Distance(string left, string right, int limit)
        {
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];
            for (var j = 0; j <= right.Length; j++) previous[j] = j;
            for (var i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                var rowBest = current[0];
                for (var j = 1; j <= right.Length; j++)
                {
                    var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                    rowBest = Math.Min(rowBest, current[j]);
                }

                if (rowBest > limit) return limit + 1;
                (previous, current) = (current, previous);
            }

            return previous[right.Length];
        }
    }
}
