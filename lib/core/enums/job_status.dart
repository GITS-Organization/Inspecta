/// A job's status, shared by every feature (00-overview.md §4). Each value
/// is a step in the request → quotation → assignment → inspection →
/// certificate → sent lifecycle; every change is written to the job's
/// history (who, when, an optional note).
enum JobStatus {
  requestReceived('request_received'),
  quoteDraft('quote_draft'),
  quoteSent('quote_sent'),
  clientCountered('client_countered'),
  quoteRejected('quote_rejected'),
  clientDeclined('client_declined'),
  quoteAccepted('quote_accepted'),
  assigned('assigned'),
  taskAccepted('task_accepted'),
  inProgress('in_progress'),
  certificateSubmitted('certificate_submitted'),
  certificateReturned('certificate_returned'),
  certificateApproved('certificate_approved'),
  sentToClient('sent_to_client');

  final String value;

  const JobStatus(this.value);

  static JobStatus? fromValue(Object? value) {
    // The API calls an accepted quote still waiting on an inspector
    // `unassigned`.
    if (value == 'unassigned') return JobStatus.quoteAccepted;
    for (final status in values) {
      if (status.value == value) return status;
    }
    return null;
  }
}
