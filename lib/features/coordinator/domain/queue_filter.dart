import '../../../core/enums/job_status.dart';
import '../../requests/domain/entities/inspection_request.dart';

/// The groups the coordinator queue's header tiles count, and — when a
/// tile is tapped — narrow the list to.
enum QueueFilter {
  /// Accepted quotation, still waiting on an inspector.
  unassigned,

  /// An inspector is assigned but hasn't started.
  scheduled,

  /// The inspector accepted the task or is on site.
  inProgress,

  /// The technical manager approved the certificate; the coordinator sends it
  /// to the client.
  toSend;

  bool matches(InspectionRequest request) => switch (this) {
    unassigned => request.isReadyToAssign,
    scheduled => request.status == JobStatus.assigned,
    inProgress => request.status == JobStatus.taskAccepted || request.status == JobStatus.inProgress,
    toSend => request.isAwaitingSend,
  };
}
