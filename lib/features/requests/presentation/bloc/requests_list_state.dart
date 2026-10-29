part of 'requests_list_cubit.dart';

enum RequestsListStatus { loading, ready, error }

/// One tab's list: the pages loaded so far, and where its loading stands.
class RequestsTabState extends Equatable {
  final RequestsListStatus status;
  final Paginated<InspectionRequest> requests;

  /// The next page is on its way.
  final bool loadingMore;

  /// The last attempt at the next page failed. Scrolling doesn't retry until
  /// the list is refreshed, so an unreachable server isn't hammered.
  final bool loadMoreFailed;
  final RequestsFailureCode? failure;

  const RequestsTabState({
    this.status = RequestsListStatus.loading,
    this.requests = const Paginated(),
    this.loadingMore = false,
    this.loadMoreFailed = false,
    this.failure,
  });

  /// Whether scrolling to the end can fetch another page.
  bool get canLoadMore =>
      status == RequestsListStatus.ready &&
      requests.hasMore &&
      !loadingMore &&
      !loadMoreFailed;

  RequestsTabState copyWith({
    Paginated<InspectionRequest>? requests,
    bool? loadingMore,
    bool? loadMoreFailed,
  }) {
    return RequestsTabState(
      status: status,
      requests: requests ?? this.requests,
      loadingMore: loadingMore ?? this.loadingMore,
      loadMoreFailed: loadMoreFailed ?? this.loadMoreFailed,
      failure: failure,
    );
  }

  @override
  List<Object?> get props => [
    status,
    requests,
    loadingMore,
    loadMoreFailed,
    failure,
  ];
}

class RequestsListState extends Equatable {
  final RequestsTab tab;

  /// The search applied to both tabs, after the typing pause.
  final String query;
  final RequestsTabState newTab;
  final RequestsTabState maybeTab;

  /// Ids of "maybe" requests being confirmed or deleted right now.
  final Set<String> busy;

  const RequestsListState({
    this.tab = RequestsTab.newTab,
    this.query = '',
    this.newTab = const RequestsTabState(),
    this.maybeTab = const RequestsTabState(),
    this.busy = const {},
  });

  RequestsTabState tabState(RequestsTab t) => switch (t) {
    RequestsTab.newTab => newTab,
    RequestsTab.maybe => maybeTab,
  };

  /// The tab being shown.
  RequestsTabState get current => tabState(tab);

  RequestsListState withTab(RequestsTab t, RequestsTabState state) => switch (t) {
    RequestsTab.newTab => copyWith(newTab: state),
    RequestsTab.maybe => copyWith(maybeTab: state),
  };

  RequestsListState copyWith({
    RequestsTab? tab,
    String? query,
    RequestsTabState? newTab,
    RequestsTabState? maybeTab,
    Set<String>? busy,
  }) {
    return RequestsListState(
      tab: tab ?? this.tab,
      query: query ?? this.query,
      newTab: newTab ?? this.newTab,
      maybeTab: maybeTab ?? this.maybeTab,
      busy: busy ?? this.busy,
    );
  }

  @override
  List<Object?> get props => [tab, query, newTab, maybeTab, busy];
}
