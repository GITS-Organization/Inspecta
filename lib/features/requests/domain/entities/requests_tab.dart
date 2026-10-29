import '../../../../core/enums/job_status.dart';
import 'request_triage.dart';

/// The inbox's two segments. "New" holds requests from senders we already
/// know; "Maybe" holds emails from senders we don't know, which the
/// coordinator confirms as requests or deletes.
enum RequestsTab { newTab, maybe }

extension RequestsTabX on RequestsTab {
  /// What the API calls the requests this tab lists.
  RequestTriage get triage => switch (this) {
    RequestsTab.newTab => RequestTriage.request,
    RequestsTab.maybe => RequestTriage.maybe,
  };

  /// Whether a job's current [JobStatus] still needs a first quote.
  static bool isNew(JobStatus status) =>
      status == JobStatus.requestReceived || status == JobStatus.quoteDraft;
}
