part of 'review_queue_cubit.dart';

enum ReviewQueueStatus { loading, ready, error }

class ReviewQueueState extends Equatable {
  final ReviewQueueStatus status;
  final List<InspectionRequest> requests;
  final Map<String, String> inspectorNames;

  /// When each certificate was submitted, read from the certificate itself —
  /// only for jobs submitted before the request carried that time (see
  /// [InspectionRequest.certificateSubmittedAt]), keyed by job id.
  final Map<String, DateTime> legacySubmittedAt;
  final RequestsFailureCode? failure;

  const ReviewQueueState({
    this.status = ReviewQueueStatus.loading,
    this.requests = const [],
    this.inspectorNames = const {},
    this.legacySubmittedAt = const {},
    this.failure,
  });

  List<InspectionRequest> _with(JobStatus status, DateTime Function(InspectionRequest) sortKey) {
    final list = requests.where((r) => r.status == status).toList()..sort((a, b) => sortKey(b).compareTo(sortKey(a)));
    return list;
  }

  static DateTime _scheduledOrReceived(InspectionRequest r) => r.scheduledAt ?? r.receivedAt;

  /// The most recently submitted certificate first. A job submitted before
  /// its request recorded the time uses the certificate's own, and until
  /// that has loaded, when it was scheduled.
  DateTime _submittedAt(InspectionRequest r) =>
      r.certificateSubmittedAt ?? legacySubmittedAt[r.id] ?? _scheduledOrReceived(r);

  List<InspectionRequest> get toReview => _with(JobStatus.certificateSubmitted, _submittedAt);

  List<InspectionRequest> get returned => _with(JobStatus.certificateReturned, _submittedAt);

  List<InspectionRequest> get sent => _with(JobStatus.sentToClient, _scheduledOrReceived);

  String? inspectorName(InspectionRequest request) => inspectorNames[request.inspectorId];

  ReviewQueueState copyWith({
    ReviewQueueStatus? status,
    List<InspectionRequest>? requests,
    Map<String, String>? inspectorNames,
    Map<String, DateTime>? legacySubmittedAt,
    RequestsFailureCode? failure,
    bool clearFailure = false,
  }) {
    return ReviewQueueState(
      status: status ?? this.status,
      requests: requests ?? this.requests,
      inspectorNames: inspectorNames ?? this.inspectorNames,
      legacySubmittedAt: legacySubmittedAt ?? this.legacySubmittedAt,
      failure: clearFailure ? null : (failure ?? this.failure),
    );
  }

  @override
  List<Object?> get props => [status, requests, inspectorNames, legacySubmittedAt, failure];
}
