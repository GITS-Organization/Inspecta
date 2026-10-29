namespace Inspecta.Domain.Enums;

public enum JobStatus
{
    RequestReceived,
    QuoteDraft,
    QuoteSent,
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