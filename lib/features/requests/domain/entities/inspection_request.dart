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
  final String? emailThreadId;
  final String? subject;
  final String? emailFrom;
  final String? emailPreview;
  final DateTime receivedAt;
  final String location;
  final DateTime? preferredDate;
  final String? accessNotes;
  final JobStatus status;
  final String? coordinatorId;
  final List<RequestItem> items;

  /// The client answered in the email thread and it hasn't been acted on
  /// yet (BR-03.6): the inbox shows "X replied" instead of the usual card.
  final bool hasUnreadClientReply;
  final String? lastReplySnippet;

  /// Set when [status] is [JobStatus.quoteRejected] (BR-03.8).
  final String? rejectReason;

  /// The inspector assigned to carry out the job, set together with
  /// [scheduledAt] when a coordinator assigns it (Feature 04).
  final String? inspectorId;
  final DateTime? scheduledAt;
  final String? assignmentNote;

  /// When the inspector last submitted this job's certificate — set again
  /// on a resubmission after the technical manager sends it back. The
  /// technical manager's Review tab lists the most recent first. Null on
  /// jobs submitted before it was recorded.
  final DateTime? certificateSubmittedAt;

  const InspectionRequest({
    required this.id,
    required this.source,
    required this.clientName,
    required this.receivedAt,
    required this.location,
    required this.status,
    required this.items,
    this.clientId,
    this.emailThreadId,
    this.subject,
    this.emailFrom,
    this.emailPreview,
    this.preferredDate,
    this.accessNotes,
    this.coordinatorId,
    this.hasUnreadClientReply = false,
    this.lastReplySnippet,
    this.rejectReason,
    this.inspectorId,
    this.scheduledAt,
    this.assignmentNote,
    this.certificateSubmittedAt,
  });

  bool get isNew => RequestsTabX.isNew(status);

  bool get isNewClient => clientId == null;

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

  @override
  List<Object?> get props => [
    id,
    source,
    clientId,
    clientName,
    emailThreadId,
    subject,
    emailFrom,
    emailPreview,
    receivedAt,
    location,
    preferredDate,
    accessNotes,
    status,
    coordinatorId,
    items,
    hasUnreadClientReply,
    lastReplySnippet,
    rejectReason,
    inspectorId,
    scheduledAt,
    assignmentNote,
    certificateSubmittedAt,
  ];
}
