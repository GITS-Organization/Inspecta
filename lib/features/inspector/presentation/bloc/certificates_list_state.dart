part of 'certificates_list_cubit.dart';

enum CertificatesListStatus { loading, ready, error }

enum CertificatesFilter { submitted, sent, all }

class CertificatesListState extends Equatable {
  final CertificatesListStatus status;
  final List<InspectionRequest> requests;
  final RequestsFailureCode? failure;
  final String query;
  final CertificatesFilter filter;

  const CertificatesListState({
    this.status = CertificatesListStatus.loading,
    this.requests = const [],
    this.failure,
    this.query = '',
    this.filter = CertificatesFilter.all,
  });

  /// Anything on the inspector's radar for a certificate — everything
  /// from "started drafting" onward. Jobs still waiting to be accepted
  /// or started (see the Tasks tab) don't have one yet.
  static bool _isCertRelevant(JobStatus status) => switch (status) {
    JobStatus.inProgress ||
    JobStatus.certificateSubmitted ||
    JobStatus.certificateReturned ||
    JobStatus.certificateApproved ||
    JobStatus.sentToClient => true,
    _ => false,
  };

  static bool _inSubmitted(JobStatus status) =>
      status == JobStatus.certificateSubmitted || status == JobStatus.certificateApproved;

  static bool _inSent(JobStatus status) => status == JobStatus.sentToClient;

  int get submittedCount => requests.where((r) => _inSubmitted(r.status)).length;

  int get sentCount => requests.where((r) => _inSent(r.status)).length;

  /// The current filter tab's bucket, then the search query, newest
  /// first.
  List<InspectionRequest> get visible {
    final bucketed = requests.where((r) => switch (filter) {
      CertificatesFilter.submitted => _inSubmitted(r.status),
      CertificatesFilter.sent => _inSent(r.status),
      CertificatesFilter.all => _isCertRelevant(r.status),
    });

    final needle = query.trim().toLowerCase();
    final matched = needle.isEmpty
        ? bucketed
        : bucketed.where(
            (r) =>
                r.clientName.toLowerCase().contains(needle) ||
                r.equipmentTitle.toLowerCase().contains(needle) ||
                certNumberFor(r).toLowerCase().contains(needle),
          );

    final list = matched.toList()..sort((a, b) => _sortKey(b).compareTo(_sortKey(a)));
    return list;
  }

  static DateTime _sortKey(InspectionRequest r) => r.scheduledAt ?? r.receivedAt;

  CertificatesListState copyWith({
    CertificatesListStatus? status,
    List<InspectionRequest>? requests,
    RequestsFailureCode? failure,
    bool clearFailure = false,
    String? query,
    CertificatesFilter? filter,
  }) {
    return CertificatesListState(
      status: status ?? this.status,
      requests: requests ?? this.requests,
      failure: clearFailure ? null : (failure ?? this.failure),
      query: query ?? this.query,
      filter: filter ?? this.filter,
    );
  }

  @override
  List<Object?> get props => [status, requests, failure, query, filter];
}
