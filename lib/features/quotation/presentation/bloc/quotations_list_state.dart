part of 'quotations_list_cubit.dart';

enum QuotationsListStatus { loading, ready, error }

class QuotationsListState extends Equatable {
  final QuotationsListStatus status;

  /// One entry per request: its current (latest, non-superseded, sent)
  /// quotation, over the pages loaded so far. Drafts don't appear here at
  /// all — see the cubit's doc.
  final Paginated<Quotation> page;

  /// The next page is on its way.
  final bool loadingMore;

  /// The last attempt at the next page failed. Scrolling doesn't retry until
  /// the board is refreshed, so an unreachable server isn't hammered.
  final bool loadMoreFailed;

  final QuoteBoardTab tab;
  final String query;

  /// Set by tapping the "Expiring soon" summary tile — a cross-cutting
  /// filter, not one of the four tabs.
  final bool onlyExpiringSoon;
  final QuotationFailureCode? failure;

  const QuotationsListState({
    this.status = QuotationsListStatus.loading,
    this.page = const Paginated(),
    this.loadingMore = false,
    this.loadMoreFailed = false,
    this.tab = QuoteBoardTab.open,
    this.query = '',
    this.onlyExpiringSoon = false,
    this.failure,
  });

  List<Quotation> get board => page.items;

  /// Whether scrolling to the end can fetch another page.
  bool get canLoadMore =>
      status == QuotationsListStatus.ready &&
      page.hasMore &&
      !loadingMore &&
      !loadMoreFailed;

  List<Quotation> _forTab(QuoteBoardTab tab, {DateTime? now}) =>
      board.where((q) => QuoteBoardTabX.of(q, now: now) == tab).toList();

  int countOf(QuoteBoardTab tab) => _forTab(tab).length;

  int get expiringSoonCount => board.where((q) => q.isExpiringSoon()).length;

  int get acceptedThisMonthCount => _acceptedThisMonth().length;

  List<Quotation> _acceptedThisMonth({DateTime? now}) {
    final effectiveNow = now ?? DateTime.now();
    return _forTab(QuoteBoardTab.accepted, now: effectiveNow).where((q) {
      final at = q.clientRespondedAt;
      return at != null && at.year == effectiveNow.year && at.month == effectiveNow.month;
    }).toList();
  }

  int get openTotalPiastres => _forTab(QuoteBoardTab.open)
      .fold(0, (sum, q) => sum + (q.totalPiastres ?? 0));

  int get acceptedThisMonthTotalPiastres =>
      _acceptedThisMonth().fold(0, (sum, q) => sum + (q.totalPiastres ?? 0));

  /// The current tab (or the expiring-soon cross-filter), then the
  /// search text (client, request id, or quote number).
  List<Quotation> get visible {
    final base = onlyExpiringSoon
        ? board.where((q) => q.isExpiringSoon()).toList()
        : _forTab(tab);
    final needle = query.trim().toLowerCase();
    if (needle.isEmpty) return base;
    return base
        .where(
          (q) =>
              q.clientName.toLowerCase().contains(needle) ||
              q.requestNumber.toLowerCase().contains(needle) ||
              q.id.toLowerCase().contains(needle),
        )
        .toList();
  }

  QuotationsListState copyWith({
    QuotationsListStatus? status,
    Paginated<Quotation>? page,
    bool? loadingMore,
    bool? loadMoreFailed,
    QuoteBoardTab? tab,
    String? query,
    bool? onlyExpiringSoon,
    QuotationFailureCode? failure,
    bool clearFailure = false,
  }) {
    return QuotationsListState(
      status: status ?? this.status,
      page: page ?? this.page,
      loadingMore: loadingMore ?? this.loadingMore,
      loadMoreFailed: loadMoreFailed ?? this.loadMoreFailed,
      tab: tab ?? this.tab,
      query: query ?? this.query,
      onlyExpiringSoon: onlyExpiringSoon ?? this.onlyExpiringSoon,
      failure: clearFailure ? null : (failure ?? this.failure),
    );
  }

  @override
  List<Object?> get props => [
    status,
    page,
    loadingMore,
    loadMoreFailed,
    tab,
    query,
    onlyExpiringSoon,
    failure,
  ];
}
