namespace Inspecta.Domain.Enums;

public static class DomainEnumParser
{
    public static JobStatus ParseJobStatus(string value) =>
        ParseSeparated<JobStatus>(value, new Dictionary<string, JobStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["request_received"] = JobStatus.RequestReceived,
            ["quote_draft"] = JobStatus.QuoteDraft,
            ["quote_sent"] = JobStatus.QuoteSent,
            ["client_countered"] = JobStatus.ClientCountered,
            ["quote_rejected"] = JobStatus.QuoteRejected,
            ["client_declined"] = JobStatus.ClientDeclined,
            ["quote_accepted"] = JobStatus.QuoteAccepted,
            ["assigned"] = JobStatus.Assigned,
            ["task_accepted"] = JobStatus.TaskAccepted,
            ["in_progress"] = JobStatus.InProgress,
            ["certificate_submitted"] = JobStatus.CertificateSubmitted,
            ["certificate_returned"] = JobStatus.CertificateReturned,
            ["certificate_approved"] = JobStatus.CertificateApproved,
            ["sent_to_client"] = JobStatus.SentToClient
        });

    public static QuoteStatus ParseQuoteStatus(string value) =>
        ParseSeparated<QuoteStatus>(value, new Dictionary<string, QuoteStatus>(StringComparer.OrdinalIgnoreCase)
        {
            ["open"] = QuoteStatus.Open,
            ["accepted"] = QuoteStatus.Accepted,
            ["rejected"] = QuoteStatus.Rejected
        });

    public static CertificateResult ParseCertificateResult(string value) =>
        ParseSeparated<CertificateResult>(value, new Dictionary<string, CertificateResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["safe_to_operate"] = CertificateResult.SafeToOperate,
            ["safe_with_conditions"] = CertificateResult.SafeWithConditions,
            ["not_safe"] = CertificateResult.NotSafe
        });

    public static ChecklistAnswer ParseChecklistAnswer(string value) =>
        ParseSeparated<ChecklistAnswer>(value, new Dictionary<string, ChecklistAnswer>(StringComparer.OrdinalIgnoreCase)
        {
            ["unanswered"] = ChecklistAnswer.Unanswered,
            ["pass"] = ChecklistAnswer.Pass,
            ["fail"] = ChecklistAnswer.Fail,
            ["na"] = ChecklistAnswer.NotApplicable,
            ["not_applicable"] = ChecklistAnswer.NotApplicable
        });

    public static RequestSource ParseRequestSource(string value) =>
        ParseSeparated<RequestSource>(value, new Dictionary<string, RequestSource>(StringComparer.OrdinalIgnoreCase)
        {
            ["manual"] = RequestSource.Manual,
            ["email"] = RequestSource.Email
        });

    public static QuoteType ParseQuoteType(string value) =>
        ParseSeparated<QuoteType>(value, new Dictionary<string, QuoteType>(StringComparer.OrdinalIgnoreCase)
        {
            ["offer"] = QuoteType.Offer,
            ["accept"] = QuoteType.Accept
        });

    public static DemoRequestType ParseDemoRequestType(string value) =>
        ParseSeparated<DemoRequestType>(value, new Dictionary<string, DemoRequestType>(StringComparer.OrdinalIgnoreCase)
        {
            ["demo"] = DemoRequestType.Demo,
            ["contact"] = DemoRequestType.Contact
        });

    private static T ParseSeparated<T>(string value, IReadOnlyDictionary<string, T> values) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A value is required.", nameof(value));
        if (values.TryGetValue(value.Trim(), out var result))
            return result;
        throw new ArgumentException($"Unsupported {typeof(T).Name} value '{value}'.", nameof(value));
    }
}
