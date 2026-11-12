using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Scaling rule: a correctness-bearing fact must not live only in the memory of one API process. Two instances would each keep
/// their own copy (login lock-outs, send locks, renewal gates), and a restart would forget it. State that lives there today is
/// on this list, each with what a second instance would need. A new one fails until it is added on purpose.
/// What counts: locks, concurrent collections and caches anywhere they are static or held by a singleton or hosted service, and
/// plain collections that a static field can change or a singleton keeps. Read-only lookup tables do not count.
/// </summary>
public sealed class ProcessLocalStateGuardTests
{
    private static readonly Assembly[] Ours =
    [
        typeof(Inspecta.Application.Services.AuthService).Assembly,
        typeof(Inspecta.Infrastructure.Data.InspectaDbContext).Assembly,
        typeof(Inspecta.Api.Controllers.AuthController).Assembly
    ];

    private static readonly Type[] SharedStateTypes =
    [
        typeof(ConcurrentDictionary<,>), typeof(ConcurrentBag<>), typeof(ConcurrentQueue<>), typeof(ConcurrentStack<>),
        typeof(SemaphoreSlim), typeof(MemoryCache)
    ];

    private static readonly Type[] PlainCollections = [typeof(Dictionary<,>), typeof(List<>), typeof(HashSet<>), typeof(Queue<>)];

    /// <summary>Type.Field, with what a second API instance would need.</summary>
    private static readonly IReadOnlySet<string> Known = new HashSet<string>
    {
        // Send locks: a second instance would allow two sends of the same quotation or certificate email at once. Needs a database lock (advisory lock or a status row).
        "QuoteDeliveryService.InFlight",
        "CertificateDeliveryService.InFlight",
        // Import retry counters: lost on restart (a stuck message is retried five more times); two instances count separately. Needs a column on the mailbox row.
        "EmailSyncBackgroundService.FailedImports",
        // Per-team promotion lock: two instances could promote the same waiting maybes together. Needs a database lock.
        "EmailSyncBackgroundService.TeamPromotion",
        // Sign-in lock-out: each instance counts failures alone, so the limit multiplies. Needs a shared store (database or Redis).
        "LoginAttemptTracker.states",
        // Microsoft access tokens and renewal gates: a second instance only refreshes more often and could spend one refresh token twice. Needs a database lock or a shared cache.
        "MailboxTokenCache.tokens",
        "MailboxTokenCache.gates"
    };

    private static bool Is(Type fieldType, Type[] kinds) =>
        kinds.Any(kind => fieldType == kind || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == kind));

    private static bool KeepsSharedState(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Any(field => Is(field.FieldType, SharedStateTypes));

    private static IEnumerable<string> StaticState(Type type) =>
        type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsLiteral && !field.Name.Contains('<'))
            .Where(field => Is(field.FieldType, SharedStateTypes) ||
                            (Is(field.FieldType, PlainCollections) && !field.IsInitOnly) ||
                            (Ours.Contains(field.FieldType.Assembly) && field.FieldType != type && KeepsSharedState(field.FieldType)))
            .Select(field => $"{type.Name}.{field.Name}");

    private static IEnumerable<string> SingletonState(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => !field.Name.Contains('<') && (Is(field.FieldType, SharedStateTypes) || Is(field.FieldType, PlainCollections)))
            .Select(field => $"{type.Name}.{field.Name}");

    /// <summary>The types the container keeps for the life of the process: <c>AddSingleton&lt;..&gt;</c> and <c>AddHostedService&lt;..&gt;</c> in Program.cs.</summary>
    private static IEnumerable<Type> LongLivedTypes(string programSource)
    {
        var names = Regex.Matches(programSource, @"Add(?:Singleton|HostedService)<(?:[A-Za-z0-9_.]+\s*,\s*)?([A-Za-z0-9_.]+)>")
            .Select(match => match.Groups[1].Value.Split('.').Last())
            .Distinct();
        return names.SelectMany(name => Ours.SelectMany(assembly => assembly.GetTypes()).Where(type => type.Name == name && type.IsClass));
    }

    internal static IReadOnlyList<string> UnregisteredLocalState(string programSource, IReadOnlySet<string> known)
    {
        var statics = Ours.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && type.GetCustomAttribute<CompilerGeneratedAttribute>() is null && !type.Name.StartsWith('<'))
            .SelectMany(StaticState);
        var singletons = LongLivedTypes(programSource).SelectMany(SingletonState);
        return statics.Concat(singletons).Where(entry => !known.Contains(entry)).Distinct().Order().ToArray();
    }

    private static string ProgramSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "InspectaBackend.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("InspectaBackend.slnx was not found above the test binaries.");
        return File.ReadAllText(Path.Combine(root, "Inspecta.Api", "Program.cs"));
    }

    [Fact]
    public void ProcessLocalStateIsOnlyWhereKnown()
    {
        var offenders = UnregisteredLocalState(ProgramSource(), Known);

        Assert.True(offenders.Count == 0,
            "Process-local state not on the list: " + string.Join(", ", offenders) +
            ". Keep it in the database or another shared store, or add it to ProcessLocalStateGuardTests.Known with a note of what a second API instance would need.");
    }

    [Fact]
    public void TheCheckCatchesStateWhenNothingIsKnown()
    {
        var found = UnregisteredLocalState(ProgramSource(), new HashSet<string>());

        Assert.Equal(Known.Order(), found);
    }

    [Fact]
    public void EveryKnownEntryStillExists()
    {
        var found = UnregisteredLocalState(ProgramSource(), new HashSet<string>());

        Assert.Empty(Known.Except(found));
    }
}
