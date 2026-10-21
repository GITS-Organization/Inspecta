part of 'review_detail_cubit.dart';

enum ReviewDetailStatus { loading, ready, notFound, error }

class ReviewDetailState extends Equatable {
  final ReviewDetailStatus status;
  final Certificate? certificate;

  final String comments;

  /// The reviewer's profile name — printed on the certificate as the person
  /// signing it. Not entered on the review screen.
  final String reviewerName;

  /// What the reviewer has drawn on this screen — only used when they have no
  /// saved signature.
  final List<List<double>> strokes;

  /// The signature saved in the reviewer's profile — empty when they haven't
  /// set one, null until it has loaded.
  final List<List<double>>? savedStrokes;

  /// Whether a signature drawn here is saved to the profile on approval.
  final bool rememberSignature;

  final bool isApproving;
  final bool isReturning;
  final int actionSeq;
  final bool lastActionSuccess;
  final bool lastActionApproved;
  final CertificateFailureCode? lastActionFailure;

  const ReviewDetailState({
    this.status = ReviewDetailStatus.loading,
    this.certificate,
    this.comments = '',
    this.reviewerName = '',
    this.strokes = const [],
    this.savedStrokes,
    this.rememberSignature = true,
    this.isApproving = false,
    this.isReturning = false,
    this.actionSeq = 0,
    this.lastActionSuccess = false,
    this.lastActionApproved = false,
    this.lastActionFailure,
  });

  bool get isBusy => isApproving || isReturning;

  static bool _hasInk(List<List<double>> strokes) => strokes.any((stroke) => stroke.length >= 4);

  bool get isSignatureLoading => savedStrokes == null;

  bool get hasSavedSignature => _hasInk(savedStrokes ?? const []);

  /// What goes on the certificate: the saved signature when there is one,
  /// otherwise what was drawn here.
  List<List<double>> get signatureStrokes => hasSavedSignature ? savedStrokes! : strokes;

  bool get isSigned => _hasInk(signatureStrokes);

  /// Signed — a certificate can't be approved without the reviewer's signature.
  bool get canApprove => certificate != null && !isSignatureLoading && isSigned;

  ReviewDetailState copyWith({
    ReviewDetailStatus? status,
    Certificate? certificate,
    String? comments,
    String? reviewerName,
    List<List<double>>? strokes,
    List<List<double>>? savedStrokes,
    bool? rememberSignature,
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
      comments: comments ?? this.comments,
      reviewerName: reviewerName ?? this.reviewerName,
      strokes: strokes ?? this.strokes,
      savedStrokes: savedStrokes ?? this.savedStrokes,
      rememberSignature: rememberSignature ?? this.rememberSignature,
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
    comments,
    reviewerName,
    strokes,
    savedStrokes,
    rememberSignature,
    isApproving,
    isReturning,
    actionSeq,
    lastActionSuccess,
    lastActionApproved,
    lastActionFailure,
  ];
}
