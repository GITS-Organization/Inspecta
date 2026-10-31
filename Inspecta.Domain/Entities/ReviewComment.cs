namespace Inspecta.Domain.Entities;

/// <summary>
/// What the technical manager wrote when sending a job back to the inspector: a general note for the job
/// (<see cref="RequestItemId"/> empty) or a comment on one machine. Every comment is kept, so the history of a job's review
/// stays readable after it is fixed.
/// </summary>
public sealed class ReviewComment
{
    private ReviewComment() { }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public InspectionRequest? Request { get; private set; }
    public Guid? RequestItemId { get; private set; }
    public Guid ReviewerId { get; private set; }
    public string? ReviewerName { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static ReviewComment Create(
        Guid requestId, Guid? requestItemId, Guid reviewerId, string? reviewerName, string text, DateTimeOffset? createdAt = null)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("Request is required.", nameof(requestId));
        if (reviewerId == Guid.Empty) throw new ArgumentException("Reviewer is required.", nameof(reviewerId));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("The comment is empty.", nameof(text));

        return new ReviewComment
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            RequestItemId = requestItemId,
            ReviewerId = reviewerId,
            ReviewerName = string.IsNullOrWhiteSpace(reviewerName) ? null : reviewerName.Trim(),
            Text = text.Trim(),
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }
}
