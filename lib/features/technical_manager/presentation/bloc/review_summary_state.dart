part of 'review_summary_cubit.dart';

enum ReviewSummaryStatus { loading, ready, error }

/// How the last "approve all" / "reject all" ended: [failure] is null when it
/// went through. [problems] lists what the API refused an approval over
/// (machines with no submitted certificate), worded for the manager.
class ReviewAction extends Equatable {
  final bool approved;
  final CertificateFailureCode? failure;
  final List<String> problems;

  const ReviewAction({required this.approved, this.failure, this.problems = const []});

  bool get succeeded => failure == null;

  @override
  List<Object?> get props => [approved, failure, problems];
}

class ReviewSummaryState extends Equatable {
  final ReviewSummaryStatus status;

  /// The job, as its `to-review` row describes it.
  final InspectionRequest request;

  /// The job's machines with a certificate, those waiting on the manager
  /// first.
  final List<MachineSummary> machines;

  /// Whether Reject / Approve all are on offer for the job.
  final bool canReview;
  final bool canApprove;

  /// Why approving isn't on offer, when [canApprove] is false.
  final List<String> reviewBlockers;

  /// Earlier comments on the job's review, newest first.
  final List<ReviewComment> reviewHistory;
  final RequestsFailureCode? loadFailure;
  final bool isApproving;
  final bool isReturning;
  final int actionSeq;
  final ReviewAction? lastAction;

  const ReviewSummaryState({
    required this.request,
    this.status = ReviewSummaryStatus.loading,
    this.machines = const [],
    this.canReview = false,
    this.canApprove = false,
    this.reviewBlockers = const [],
    this.reviewHistory = const [],
    this.loadFailure,
    this.isApproving = false,
    this.isReturning = false,
    this.actionSeq = 0,
    this.lastAction,
  });

  bool get isBusy => isApproving || isReturning;

  ReviewSummaryState copyWith({
    ReviewSummaryStatus? status,
    InspectionRequest? request,
    List<MachineSummary>? machines,
    bool? canReview,
    bool? canApprove,
    List<String>? reviewBlockers,
    List<ReviewComment>? reviewHistory,
    RequestsFailureCode? loadFailure,
    bool? isApproving,
    bool? isReturning,
    int? actionSeq,
    ReviewAction? lastAction,
  }) {
    return ReviewSummaryState(
      status: status ?? this.status,
      request: request ?? this.request,
      machines: machines ?? this.machines,
      canReview: canReview ?? this.canReview,
      canApprove: canApprove ?? this.canApprove,
      reviewBlockers: reviewBlockers ?? this.reviewBlockers,
      reviewHistory: reviewHistory ?? this.reviewHistory,
      loadFailure: loadFailure ?? this.loadFailure,
      isApproving: isApproving ?? this.isApproving,
      isReturning: isReturning ?? this.isReturning,
      actionSeq: actionSeq ?? this.actionSeq,
      lastAction: lastAction ?? this.lastAction,
    );
  }

  @override
  List<Object?> get props => [
    status,
    request,
    machines,
    canReview,
    canApprove,
    reviewBlockers,
    reviewHistory,
    loadFailure,
    isApproving,
    isReturning,
    actionSeq,
    lastAction,
  ];
}
