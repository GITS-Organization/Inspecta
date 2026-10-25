namespace Inspecta.Domain.Entities;

public sealed class InspectorSignature
{
    private InspectorSignature() { }

    public Guid UserId { get; private set; }
    public string? LegacyId { get; private set; }
    public IReadOnlyList<SignatureStroke> Strokes { get; private set; } = [];
    public DateTimeOffset UpdatedAt { get; private set; }

    public static InspectorSignature Create(
        Guid userId,
        IEnumerable<SignatureStroke> strokes,
        DateTimeOffset? updatedAt = null,
        string? legacyId = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        ArgumentNullException.ThrowIfNull(strokes);

        var signatureStrokes = strokes.ToArray();
        if (signatureStrokes.Length == 0)
            throw new ArgumentException("A signature must contain at least one stroke.", nameof(strokes));

        return new InspectorSignature
        {
            UserId = userId,
            LegacyId = legacyId,
            Strokes = signatureStrokes,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow
        };
    }

    public void Replace(IEnumerable<SignatureStroke> strokes, DateTimeOffset? updatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        var signatureStrokes = strokes.ToArray();
        if (signatureStrokes.Length == 0)
            throw new ArgumentException("A signature must contain at least one stroke.", nameof(strokes));

        Strokes = signatureStrokes;
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }
}
