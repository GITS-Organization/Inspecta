namespace Inspecta.Domain.Enums;

public enum JobStatus
{
    RequestReceived,
    QuoteDraft,
    QuoteSent,

    /// <summary>A revised offer (a counter offer: the second revision or later) was sent and the client has not answered.</summary>
    CounterOfferSent,
    ClientCountered,
    QuoteRejected,
    ClientDeclined,
    Unassigned,
    Assigned,
    TaskAccepted,
    InProgress,
    CertificateSubmitted,
    CertificateReturned,
    CertificateApproved,
    SentToClient
}