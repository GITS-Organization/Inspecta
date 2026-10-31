import 'package:equatable/equatable.dart';

import 'machine_status.dart';

/// How many of a job's machines have reached each stage.
class MachineProgress extends Equatable {
  final int total;
  final int submitted;
  final int approved;
  final int delivered;

  const MachineProgress({
    this.total = 0,
    this.submitted = 0,
    this.approved = 0,
    this.delivered = 0,
  });

  @override
  List<Object?> get props => [total, submitted, approved, delivered];
}

/// One machine of a job, with where its certificate stands and what the
/// current user may do with it.
class MachineSummary extends Equatable {
  final String requestItemId;
  final String name;
  final int quantity;
  final String? serialNumber;
  final String? templateKey;
  final MachineStatus status;
  final int checklistTotal;
  final int checklistAnswered;

  /// The technical manager's note when the machine was sent back.
  final String? reviewNote;

  /// Set once the machine's certificate is approved.
  final String? certificateNumber;
  final bool canEdit;
  final bool canReview;

  /// What the review screen shows of the certificate: the machine's kind and
  /// template, its important data, the standards printed on it, the
  /// inspection dates and the verdict. All optional on the wire.
  final String? type;
  final String? templateName;
  final String? manufacturer;
  final String? capacity;
  final String? model;
  final List<String> inspectionStandards;
  final String? examinationDate;
  final String? nextExaminationDate;

  /// `satisfactory` or `unsatisfactory`.
  final String? result;

  const MachineSummary({
    required this.requestItemId,
    required this.name,
    required this.quantity,
    required this.status,
    this.serialNumber,
    this.templateKey,
    this.checklistTotal = 0,
    this.checklistAnswered = 0,
    this.reviewNote,
    this.certificateNumber,
    this.canEdit = false,
    this.canReview = false,
    this.type,
    this.templateName,
    this.manufacturer,
    this.capacity,
    this.model,
    this.inspectionStandards = const [],
    this.examinationDate,
    this.nextExaminationDate,
    this.result,
  });

  @override
  List<Object?> get props => [
    requestItemId,
    name,
    quantity,
    serialNumber,
    templateKey,
    status,
    checklistTotal,
    checklistAnswered,
    reviewNote,
    certificateNumber,
    canEdit,
    canReview,
    type,
    templateName,
    manufacturer,
    capacity,
    model,
    inspectionStandards,
    examinationDate,
    nextExaminationDate,
    result,
  ];
}

/// One earlier comment on a job's review: what the technical manager wrote
/// when sending it back.
class ReviewComment extends Equatable {
  final String text;
  final String? author;
  final DateTime? at;

  const ReviewComment({required this.text, this.author, this.at});

  @override
  List<Object?> get props => [text, author, at];
}

/// A job and the machines to inspect on it. The `can…` flags already take the
/// current user and the job's state into account.
class JobMachines extends Equatable {
  final String requestId;
  final String requestStatus;
  final String clientName;
  final String location;
  final DateTime? scheduledAt;
  final MachineProgress progress;
  final bool canStartInspection;
  final bool canDeliver;
  final List<MachineSummary> machines;

  /// The technical manager's buttons for the whole job: Reject shows when
  /// [canReview], Approve when [canApprove]. While approving isn't possible,
  /// [reviewBlockers] says why.
  final bool canReview;
  final bool canApprove;
  final List<String> reviewBlockers;

  /// Earlier comments on the job's review, newest first.
  final List<ReviewComment> reviewHistory;

  const JobMachines({
    required this.requestId,
    required this.requestStatus,
    required this.clientName,
    required this.location,
    required this.progress,
    required this.machines,
    this.scheduledAt,
    this.canStartInspection = false,
    this.canDeliver = false,
    this.canReview = false,
    this.canApprove = false,
    this.reviewBlockers = const [],
    this.reviewHistory = const [],
  });

  @override
  List<Object?> get props => [
    requestId,
    requestStatus,
    clientName,
    location,
    scheduledAt,
    progress,
    canStartInspection,
    canDeliver,
    machines,
    canReview,
    canApprove,
    reviewBlockers,
    reviewHistory,
  ];
}
