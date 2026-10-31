part of 'review_queue_cubit.dart';

enum ReviewQueueStatus { loading, ready, error }

class ReviewQueueState extends Equatable {
  final ReviewQueueStatus status;
  final List<InspectionRequest> requests;

  /// The `to-review` list: jobs with certificates waiting on a signature.
  final List<InspectionRequest> toReviewRequests;
  final RequestsFailureCode? failure;

  const ReviewQueueState({
    this.status = ReviewQueueStatus.loading,
    this.requests = const [],
    this.toReviewRequests = const [],
    this.failure,
  });

  List<InspectionRequest> _with(JobStatus status, DateTime Function(InspectionRequest) sortKey) {
    final list = requests.where((r) => r.status == status).toList()..sort((a, b) => sortKey(b).compareTo(sortKey(a)));
    return list;
  }

  static DateTime _scheduledOrReceived(InspectionRequest r) => r.scheduledAt ?? r.receivedAt;

  /// The most recently submitted certificate first: the queue row carries no
  /// submission time of its own, so the job's last update stands in.
  DateTime _submittedAt(InspectionRequest r) => r.certificateSubmittedAt ?? r.updatedAt ?? _scheduledOrReceived(r);

  List<InspectionRequest> get toReview =>
      [...toReviewRequests]..sort((a, b) => _submittedAt(b).compareTo(_submittedAt(a)));

  List<InspectionRequest> get returned => _with(JobStatus.certificateReturned, _submittedAt);

  /// Approved certificates, whether the coordinator has sent them on yet or
  /// not — newest first.
  List<InspectionRequest> get sent {
    final list = requests
        .where((r) => r.status == JobStatus.certificateApproved || r.status == JobStatus.sentToClient)
        .toList()
      ..sort((a, b) => _scheduledOrReceived(b).compareTo(_scheduledOrReceived(a)));
    return list;
  }

  /// The inspector's name as the queue row names them.
  String? inspectorName(InspectionRequest request) => request.inspectorName;

  ReviewQueueState copyWith({
    ReviewQueueStatus? status,
    List<InspectionRequest>? requests,
    List<InspectionRequest>? toReviewRequests,
    RequestsFailureCode? failure,
    bool clearFailure = false,
  }) {
    return ReviewQueueState(
      status: status ?? this.status,
      requests: requests ?? this.requests,
      toReviewRequests: toReviewRequests ?? this.toReviewRequests,
      failure: clearFailure ? null : (failure ?? this.failure),
    );
  }

  @override
  List<Object?> get props => [status, requests, toReviewRequests, failure];
}
