part of 'review_detail_cubit.dart';

enum ReviewDetailStatus { loading, ready, error }

class ReviewDetailState extends Equatable {
  final ReviewDetailStatus status;
  final Certificate? certificate;

  /// Whether the API lets the current user review this machine now.
  final bool canReview;

  /// The reviewer's profile name — printed on the certificate as the person
  /// signing it. Not entered on the review screen.
  final String reviewerName;

  final bool isApproving;
  final bool isReturning;
  final int actionSeq;
  final bool lastActionSuccess;
  final bool lastActionApproved;
  final CertificateFailureCode? lastActionFailure;

  const ReviewDetailState({
    this.status = ReviewDetailStatus.loading,
    this.certificate,
    this.canReview = false,
    this.reviewerName = '',
    this.isApproving = false,
    this.isReturning = false,
    this.actionSeq = 0,
    this.lastActionSuccess = false,
    this.lastActionApproved = false,
    this.lastActionFailure,
  });

  bool get isBusy => isApproving || isReturning;

  /// A certificate can't be approved when the API doesn't let the reviewer
  /// review it.
  bool get canApprove => certificate != null && canReview;

  ReviewDetailState copyWith({
    ReviewDetailStatus? status,
    Certificate? certificate,
    bool? canReview,
    String? reviewerName,
    bool? isApproving,
    bool? isReturning,
    int? actionSeq,
    bool? lastActionSuccess,
    bool? lastActionApproved,
    CertificateFailureCode? lastActionFailure,
  }) {
    return ReviewDetailState(
      status: status ?? this.status,
      certificate: certificate ?? this.certificate,
      canReview: canReview ?? this.canReview,
      reviewerName: reviewerName ?? this.reviewerName,
      isApproving: isApproving ?? this.isApproving,
      isReturning: isReturning ?? this.isReturning,
      actionSeq: actionSeq ?? this.actionSeq,
      lastActionSuccess: lastActionSuccess ?? this.lastActionSuccess,
      lastActionApproved: lastActionApproved ?? this.lastActionApproved,
      lastActionFailure: lastActionFailure ?? this.lastActionFailure,
    );
  }

  @override
  List<Object?> get props => [
    status,
    certificate,
    canReview,
    reviewerName,
    isApproving,
    isReturning,
    actionSeq,
    lastActionSuccess,
    lastActionApproved,
    lastActionFailure,
  ];
}
