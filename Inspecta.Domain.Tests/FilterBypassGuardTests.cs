using System.Text.RegularExpressions;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Tenancy rule 2: <c>IgnoreQueryFilters()</c> switches the company separation off for one query. It is needed in a few places
/// (the mail sync reads every team, sign-in looks a user up before any tenant is known, public pages), so each use is on
/// this register. A new use, in any file, fails until someone adds it here on purpose and a reviewer sees it.
/// </summary>
public sealed class FilterBypassGuardTests
{
    /// <summary>File name and how many times it calls IgnoreQueryFilters today.</summary>
    private static readonly IReadOnlyDictionary<string, int> Register = new Dictionary<string, int>
    {
        ["EfCertificateFileRepository.cs"] = 2,
        ["EfClientMailRuleRepository.cs"] = 3,
        ["EfClientReplyRepository.cs"] = 6,
        ["EfClientRepository.cs"] = 2,
        ["EfEmailSyncRepository.cs"] = 10,
        ["EfEquipmentCatalog.cs"] = 1,
        ["EfEquipmentRepository.cs"] = 2,
        ["EfUserRepository.cs"] = 3
    };

    private static readonly string[] SourceFolders = ["Inspecta.Application", "Inspecta.Infrastructure", "Inspecta.Api"];

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "InspectaBackend.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException(
            "InspectaBackend.slnx was not found above the test binaries, so the source cannot be scanned. Run the tests from inside the repository.");
    }

    private static IEnumerable<(string File, int Uses)> Uses(string root) =>
        SourceFolders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .Select(path => (File: Path.GetFileName(path), Uses: Regex.Matches(File.ReadAllText(path), "IgnoreQueryFilters").Count))
            .Where(entry => entry.Uses > 0);

    internal static IReadOnlyList<string> UnregisteredBypasses(string root, IReadOnlyDictionary<string, int> register) =>
        Uses(root)
            .Where(entry => !register.TryGetValue(entry.File, out var allowed) || entry.Uses > allowed)
            .Select(entry => $"{entry.File}: {entry.Uses} use(s), registered {(register.TryGetValue(entry.File, out var allowed) ? allowed : 0)}")
            .Order()
            .ToArray();

    /// <summary>Register entries that are too high or point at a file that no longer bypasses: lower or remove them.</summary>
    internal static IReadOnlyList<string> StaleRegisterEntries(string root, IReadOnlyDictionary<string, int> register)
    {
        var actual = Uses(root).ToDictionary(entry => entry.File, entry => entry.Uses);
        return register
            .Where(entry => !actual.TryGetValue(entry.Key, out var uses) || uses < entry.Value)
            .Select(entry => $"{entry.Key}: registered {entry.Value}, found {(actual.TryGetValue(entry.Key, out var uses) ? uses : 0)}")
            .Order()
            .ToArray();
    }

    [Fact]
    public void FilterBypassesAreOnlyWhereRegistered()
    {
        var offenders = UnregisteredBypasses(Root(), Register);

        Assert.True(offenders.Count == 0,
            "IgnoreQueryFilters is used where it is not registered: " + string.Join("; ", offenders) +
            ". Scope the query by ManagerId instead, or, if the bypass is needed, add it to FilterBypassGuardTests.Register and say why in the pull request.");
    }

    [Fact]
    public void TheRegisterHasNoStaleEntries()
    {
        var stale = StaleRegisterEntries(Root(), Register);

        Assert.True(stale.Count == 0, "Lower or remove in FilterBypassGuardTests.Register: " + string.Join("; ", stale));
    }

    [Fact]
    public void TheCheckCatchesBypassesWhenNothingIsRegistered()
    {
        var found = UnregisteredBypasses(Root(), new Dictionary<string, int>());

        Assert.Equal(Register.Count, found.Count);
        Assert.Equal(Register.Values.Sum(), Uses(Root()).Sum(entry => entry.Uses));
    }

    [Fact]
    public void ARegisterEntryForAFileThatDoesNotBypassIsReportedAsStale()
    {
        var stale = StaleRegisterEntries(Root(), new Dictionary<string, int> { ["EfRequestRepository.cs"] = 1 });

        Assert.Equal(["EfRequestRepository.cs: registered 1, found 0"], stale);
    }
}
