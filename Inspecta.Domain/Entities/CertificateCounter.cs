namespace Inspecta.Domain.Entities;

/// <summary>The last certificate number given in a year to a team; the next certificate gets the next one.</summary>
public sealed class CertificateCounter
{
    private CertificateCounter() { }

    public Guid Id { get; private set; }
    public Guid ManagerId { get; private set; }
    public int Year { get; private set; }
    public int LastNumber { get; private set; }

    public static CertificateCounter Create(Guid managerId, int year) =>
        new() { Id = Guid.NewGuid(), ManagerId = managerId, Year = year };

    /// <summary>Takes the next number. Two certificates approved at once cannot take the same one: the save of the second fails and retries.</summary>
    public int Increment() => ++LastNumber;

    /// <summary>The number as it is printed, for example <c>CERT-2026-0042</c>.</summary>
    public static string Format(int year, int number) => $"CERT-{year}-{number:D4}";
}
