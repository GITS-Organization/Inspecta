using System.Text.RegularExpressions;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Before companies existed the id that keys every row (<c>ManagerId</c>) was also the id of a real user, the technical manager, and the
/// code leaned on that. It no longer is: a company id is the id of a company. This test fails when a user is looked up with an id that
/// names a company or a team, which is the mistake the old habit invites. Ask the company (<c>ICompanyRepository</c>, <c>ICompanyGate</c>)
/// or the user's own <c>CompanyId</c> instead.
/// </summary>
public sealed class CompanyIdGuardTests
{
    private static readonly Regex UserLookup = new(
        @"\b(userRepository|_userRepository|users|userRepo)\.GetByIdAsync\(\s*([^),]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CompanyNamed = new(
        @"(managerid|companyid|managerIdValue|\bteamid\b|\bteam\b|\.managerid)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>File name and why it may still do it. Empty on purpose: add a line only with a reason a reviewer accepts.</summary>
    private static readonly IReadOnlyDictionary<string, string> Allowed = new Dictionary<string, string>();

    private static readonly string[] SourceFolders = ["Inspecta.Application", "Inspecta.Infrastructure", "Inspecta.Api"];

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "InspectaBackend.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The solution folder was not found.");
    }

    private static IEnumerable<(string File, int Line, string Text)> Offences(Func<string, bool> skipFile) =>
        SourceFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Root(), folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}") &&
                           !skipFile(Path.GetFileName(path)))
            .SelectMany(path => File.ReadAllLines(path)
                .Select((text, index) => (File: Path.GetFileName(path), Line: index + 1, Text: text.Trim()))
                .Where(item => UserLookup.Matches(item.Text).Any(match => CompanyNamed.IsMatch(match.Groups[2].Value))));

    [Fact]
    public void NoUserIsLookedUpWithACompanyOrTeamId()
    {
        var offences = Offences(file => Allowed.ContainsKey(file)).Select(item => $"{item.File}:{item.Line}: {item.Text}").ToArray();

        Assert.True(offences.Length == 0,
            "A user is looked up with an id that names a company: " + string.Join(" | ", offences) +
            ". A company id is not a user id; ask ICompanyRepository or ICompanyGate, or use the user's own CompanyId.");
    }

    [Theory]
    [InlineData("var manager = await userRepository.GetByIdAsync(request.ManagerId, ct);")]
    [InlineData("var team = await users.GetByIdAsync(managerId, cancellationToken);")]
    [InlineData("await _userRepository.GetByIdAsync(companyId);")]
    public void TheCheckSeesTheOldHabit(string line) =>
        Assert.Contains(UserLookup.Matches(line), match => CompanyNamed.IsMatch(match.Groups[2].Value));

    [Theory]
    [InlineData("var reviewer = await userRepository.GetByIdAsync(reviewerId, ct);")]
    [InlineData("var company = await companies.GetByIdAsync(companyId, tracking: false, ct);")]
    [InlineData("var user = await users.GetByIdAsync(actorId, ct);")]
    public void TheCheckLeavesCompanyLookupsAndOtherUserLookupsAlone(string line) =>
        Assert.DoesNotContain(UserLookup.Matches(line), match => CompanyNamed.IsMatch(match.Groups[2].Value));
}
