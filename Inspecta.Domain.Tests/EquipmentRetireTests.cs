using Inspecta.Application.Interfaces;
using Inspecta.Application.Services;
using Inspecta.Domain.Entities;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>A machine that was scrapped or sold is marked out of service: it leaves every renewal, and its history stays.</summary>
public sealed class EquipmentRetireTests
{
    private static Item Machine() => Item.Create(Guid.NewGuid(), "Overhead crane 10 t");

    [Fact]
    public void RetiringAMachineMarksItOutOfServiceWithTheDateAndKeepsItsInspectionDates()
    {
        var machine = Machine();
        machine.RecordInspection(new DateOnly(2025, 12, 2), new DateOnly(2026, 12, 2), CertificateResult.SafeToOperate);
        var before = DateTimeOffset.UtcNow;

        machine.Retire();

        Assert.Equal(EquipmentStatus.Retired, machine.Status);
        Assert.InRange(machine.RetiredAt!.Value, before, DateTimeOffset.UtcNow);
        Assert.Equal(new DateOnly(2025, 12, 2), machine.LastInspectedOn);
        Assert.NotNull(machine.NextInspectionDueAt);
    }

    [Fact]
    public void RetiringTwiceKeepsTheFirstDate()
    {
        var machine = Machine();
        machine.Retire();
        var first = machine.RetiredAt;

        machine.Retire();

        Assert.Equal(first, machine.RetiredAt);
    }

    [Fact]
    public void ReactivatingBringsTheMachineBack()
    {
        var machine = Machine();
        machine.Retire();

        machine.Reactivate();

        Assert.Equal(EquipmentStatus.Active, machine.Status);
        Assert.Null(machine.RetiredAt);
    }

    [Fact]
    public void AnArchivedMachineCanNeitherBeRetiredNorReactivated()
    {
        var machine = Machine();
        machine.Archive();

        Assert.Throws<InvalidOperationException>(machine.Retire);
        Assert.Throws<InvalidOperationException>(machine.Reactivate);
    }

    [Fact]
    public async Task TheServiceAnswersWithTheStatusAndLeavesTheRestAsItWas()
    {
        var machine = Machine();
        machine.UpdateDetails("Overhead crane 10 t", null, null, null, "OC-1021", "10 t");
        var repository = new OneMachine(machine);
        var service = new EquipmentService(repository);

        var retired = await service.RetireAsync(machine.Id);
        Assert.Equal("retired", retired.Status);
        Assert.NotNull(retired.RetiredAt);
        Assert.Equal("OC-1021", retired.SerialNumber);
        Assert.Equal(1, repository.Saves);

        // Calling it again does nothing new.
        var again = await service.RetireAsync(machine.Id);
        Assert.Equal(retired.RetiredAt, again.RetiredAt);

        var active = await service.ReactivateAsync(machine.Id);
        Assert.Equal("active", active.Status);
        Assert.Null(active.RetiredAt);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RetireAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReactivateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task TheListAndTheDetailsOfAnyMachineCarryItsStatus()
    {
        var machine = Machine();
        var service = new EquipmentService(new OneMachine(machine));

        var details = await service.GetByIdAsync(machine.Id);

        Assert.Equal("active", details!.Status);
        Assert.Null(details.RetiredAt);
    }

    private sealed class OneMachine(Item machine) : IEquipmentRepository
    {
        public int Saves { get; private set; }
        public Task AddAsync(Item equipment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Item?> GetByIdAsync(Guid equipmentId, bool tracking, CancellationToken cancellationToken = default) =>
            Task.FromResult<Item?>(equipmentId == machine.Id ? machine : null);
        public Task<IReadOnlyList<Item>> ListAsync(Guid? clientId, bool includeArchived, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Item>>([machine]);
        public Task<(IReadOnlyList<Item> Items, int Total)> ListPageAsync(Guid? clientId, bool includeArchived, int offset, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<Item>, int)>(([machine], 1));
        public Task<int> CountUsagesAsync(Guid equipmentId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task RemoveAsync(Item equipment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) { Saves++; return Task.CompletedTask; }
        public Task<bool> ClientExistsAsync(Guid clientId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
