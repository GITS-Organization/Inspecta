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

  const ReviewDetailState({
    this.status = ReviewDetailStatus.loading,
    this.certificate,
    this.canReview = false,
    this.reviewerName = '',
  });

  ReviewDetailState copyWith({
    ReviewDetailStatus? status,
    Certificate? certificate,
    bool? canReview,
    String? reviewerName,
  }) {
    return ReviewDetailState(
      status: status ?? this.status,
      certificate: certificate ?? this.certificate,
      canReview: canReview ?? this.canReview,
      reviewerName: reviewerName ?? this.reviewerName,
    );
  }

  @override
  List<Object?> get props => [status, certificate, canReview, reviewerName];
}
