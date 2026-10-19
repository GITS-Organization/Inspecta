part of 'quotation_detail_cubit.dart';

enum QuotationDetailStatus { loading, ready, error }

class QuotationDetailState extends Equatable {
  final QuotationDetailStatus status;

  /// Every version for this request, newest first (the timeline).
  final List<Quotation> versions;
  final QuotationFailureCode? failure;

  /// The request itself — fetched once, separately from [versions], for
  /// the info card and the timeline's "Request received" step. Null until
  /// it loads (or if it fails); the rest of the screen doesn't wait on it.
  final InspectionRequest? request;

  final bool isSubmitting;
  final int actionSeq;
  final bool lastActionSuccess;
  final QuotationFailureCode? lastActionFailure;

  const QuotationDetailState({
    this.status = QuotationDetailStatus.loading,
    this.versions = const [],
    this.failure,
    this.request,
    this.isSubmitting = false,
    this.actionSeq = 0,
    this.lastActionSuccess = false,
    this.lastActionFailure,
  });

  /// The version coordinators act on: the newest one that isn't
  /// superseded (a draft counts too, so a resumed draft still shows).
  Quotation? get current => versions.isEmpty
      ? null
      : versions.firstWhere(
          (q) => q.status != QuotationStatus.superseded,
          orElse: () => versions.first,
        );

  QuotationDetailState copyWith({
    QuotationDetailStatus? status,
    List<Quotation>? versions,
    QuotationFailureCode? failure,
    InspectionRequest? request,
    bool? isSubmitting,
    int? actionSeq,
    bool? lastActionSuccess,
    QuotationFailureCode? lastActionFailure,
    bool clearFailure = false,
  }) {
    return QuotationDetailState(
      status: status ?? this.status,
      versions: versions ?? this.versions,
      failure: failure ?? this.failure,
      request: request ?? this.request,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      actionSeq: actionSeq ?? this.actionSeq,
      lastActionSuccess: lastActionSuccess ?? this.lastActionSuccess,
      lastActionFailure: clearFailure ? null : (lastActionFailure ?? this.lastActionFailure),
    );
  }

  @override
  List<Object?> get props => [
    status,
    versions,
    failure,
    request,
    isSubmitting,
    actionSeq,
    lastActionSuccess,
    lastActionFailure,
  ];
}
