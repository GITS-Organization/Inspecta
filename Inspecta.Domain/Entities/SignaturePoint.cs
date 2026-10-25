namespace Inspecta.Domain.Entities;

public readonly record struct SignaturePoint(double X, double Y)
{
    public static SignaturePoint Create(double x, double y)
    {
        if (x is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(x), "Signature coordinates must be normalized to the 0..1 range.");
        if (y is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(y), "Signature coordinates must be normalized to the 0..1 range.");

        return new SignaturePoint(x, y);
    }
}
