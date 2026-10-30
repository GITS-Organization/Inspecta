import 'package:equatable/equatable.dart';

import '../../../../core/enums/job_status.dart';
import 'request_item.dart';
import 'request_source.dart';
import 'requests_tab.dart';

/// The `Job` at its request/quotation stage (00-overview.md §7). One record
/// per client email thread (BR-02.1, BR-02.2), or logged manually
/// (US-02.5).
class InspectionRequest extends Equatable {
  final String id;
  final RequestSource source;

  /// Null until the sender is matched to a known client (BR-02.4) — the
  /// coordinator must confirm or create the client before quoting.
  final String? clientId;
  final String clientName;
  final String? subject;
  final String? emailFrom;

  /// The sender's display name from the email header, e.g. "Seif Mohamed".
  final String? emailFromName;
  final String? emailPreview;
  final DateTime receivedAt;
  final String location;
  final DateTime? preferredDate;
  final String? accessNotes;
  final JobStatus status;
  final String? coordinatorId;
  final List<RequestItem> items;

  /// Set when [status] is [JobStatus.quoteRejected] (BR-03.8).
  final String? rejectReason;

  /// The inspector assigned to carry out the job, set together with
  /// [scheduledAt] when a coordinator assigns it (Feature 04).
  final String? inspectorId;

  /// The assigned inspector's name when the API reports it (the coordinator
  /// queue's rows do); null otherwise.
  final String? inspectorName;
  final DateTime? scheduledAt;
  final String? assignmentNote;

  /// When the inspector last submitted this job's certificate — set again
  /// on a resubmission after the technical manager sends it back. The
  /// technical manager's Review tab lists the most recent first. Null on
  /// jobs submitted before it was recorded.
  final DateTime? certificateSubmittedAt;

  /// When the API last changed the request; sent back on an update so the
  /// API can refuse it if the request changed meanwhile. Null for requests
  /// that didn't come from the API.
  final DateTime? updatedAt;

  const InspectionRequest({
    required this.id,
    required this.source,
    required this.clientName,
    required this.receivedAt,
    required this.location,
    required this.status,
    required this.items,
    this.clientId,
    this.subject,
    this.emailFrom,
    this.emailFromName,
    this.emailPreview,
    this.preferredDate,
    this.accessNotes,
    this.coordinatorId,
    this.rejectReason,
    this.inspectorId,
    this.inspectorName,
    this.scheduledAt,
    this.assignmentNote,
    this.certificateSubmittedAt,
    this.updatedAt,
  });

  bool get isNew => RequestsTabX.isNew(status);

  bool get isNewClient => clientId == null;

  /// Who the request is from: the client once matched, otherwise whoever
  /// sent the email — an unmatched sender has no client name yet.
  String get senderLabel {
    if (clientName.isNotEmpty) return clientName;
    return emailFromName?.isNotEmpty == true ? emailFromName! : emailFrom ?? '';
  }

  /// The client accepted a quote but no inspector has been assigned yet —
  /// the coordinator's "ready to assign" queue (Feature 04 §5).
  bool get isReadyToAssign => status == JobStatus.quoteAccepted;

  /// An inspector has been assigned and the job hasn't been sent to the
  /// client yet — still on the coordinator's radar as a scheduled/running
  /// job.
  bool get isAssignedOrLater =>
      status == JobStatus.assigned ||
      status == JobStatus.taskAccepted ||
      status == JobStatus.inProgress;

  /// The technical manager has approved and signed the certificate and it is
  /// back with the coordinator to send to the client.
  bool get isAwaitingSend => status == JobStatus.certificateApproved;

  /// Card title: the first item's equipment, e.g. "Overhead crane — 10 t".
  /// A request always has at least one item once past intake, but a
  /// mid-edit manual draft could momentarily have none.
  String get equipmentTitle =>
      items.isEmpty ? subject ?? location : items.first.displayTitle;

  int get totalUnits => items.fold(0, (sum, item) => sum + item.quantity);

  /// BR-02.8: New for more than 24 hours.
  bool isWaitingOver24h({DateTime? now}) =>
      isNew && (now ?? DateTime.now()).difference(receivedAt).inHours >= 24;

  /// Required before a quote can be sent (BR-02.5).
  bool get isReadyToQuote =>
      clientId != null && items.isNotEmpty && location.isNotEmpty;

  /// Whether the reply panel (type, prices, validity, PDF proposal) applies:
  /// a first quote while the request is New, or a new version once the
  /// client has countered the last one.
  bool get isQuotable =>
      (isNew || status == JobStatus.clientCountered) && isReadyToQuote;

  @override
  List<Object?> get props => [
    id,
    source,
    clientId,
    clientName,
    subject,
    emailFrom,
    emailFromName,
    emailPreview,
    receivedAt,
    location,
    preferredDate,
    accessNotes,
    status,
    coordinatorId,
    items,
    rejectReason,
    inspectorId,
    inspectorName,
    scheduledAt,
    assignmentNote,
    certificateSubmittedAt,
    updatedAt,
  ];
}
