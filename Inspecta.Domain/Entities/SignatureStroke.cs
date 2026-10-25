namespace Inspecta.Domain.Entities;

public sealed record SignatureStroke(IReadOnlyList<SignaturePoint> Points)
{
    public static SignatureStroke Create(IEnumerable<SignaturePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var normalizedPoints = points.ToArray();
        if (normalizedPoints.Length < 2)
            throw new ArgumentException("A signature stroke must contain at least two points.", nameof(points));
        if (normalizedPoints.Any(point => point.X is < 0 or > 1 || point.Y is < 0 or > 1))
            throw new ArgumentOutOfRangeException(nameof(points), "Signature coordinates must be normalized to the 0..1 range.");

        return new SignatureStroke(normalizedPoints);
    }

    public static SignatureStroke FromFlatCoordinates(IEnumerable<double> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        var values = coordinates.ToArray();
        if (values.Length < 4 || values.Length % 2 != 0)
            throw new ArgumentException("A signature stroke must contain x/y coordinate pairs.", nameof(coordinates));

        var points = new SignaturePoint[values.Length / 2];
        for (var index = 0; index < values.Length; index += 2)
            points[index / 2] = SignaturePoint.Create(values[index], values[index + 1]);

        return Create(points);
    }
}
