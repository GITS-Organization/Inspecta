using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;

namespace Inspecta.Domain.Tests;

/// <summary>An in-memory list of companies for the tests that use fake repositories.</summary>
public sealed class FakeCompanyRepository(params Company[] companies) : ICompanyRepository
{
    public List<Company> Items { get; } = [.. companies];

    public Task<Company?> GetByIdAsync(Guid id, bool tracking = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.SingleOrDefault(company => company.Id == id));

    public Task<IReadOnlyDictionary<Guid, Company>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Company>>(Items.Where(company => ids.Contains(company.Id)).ToDictionary(company => company.Id));

    public Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Company>>(Items.OrderBy(company => company.Name).ToArray());

    public Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        Items.Add(company);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public static class TestCompanies
{
    /// <summary>A company with the given id (a legacy-style company: same id as its first technical manager).</summary>
    public static Company Of(Guid id, string name = "Acme") => Company.Create(name, id: id);

    public static CompanyGate GateFor(params Company[] companies) => new(new FakeCompanyRepository(companies));
}
