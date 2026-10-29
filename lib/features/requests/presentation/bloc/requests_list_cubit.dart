import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/base/paginated.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/requests_failure.dart';
import '../../domain/entities/requests_tab.dart';
import '../../domain/usecases/confirm_request.dart';
import '../../domain/usecases/delete_request.dart';
import '../../domain/usecases/get_triaged_requests.dart';

part 'requests_list_state.dart';

/// Backs the requests inbox: the New and Maybe lists, each loaded a page at a
/// time from the API, with the search and tab applied to them.
class RequestsListCubit extends Cubit<RequestsListState> {
  final GetTriagedRequests _getTriagedRequests;
  final ConfirmRequest _confirmRequest;
  final DeleteRequest _deleteRequest;
  final Duration _searchDelay;

  Timer? _searchTimer;
  bool _started = false;

  /// The tabs whose first page has been asked for. A tab not in here has
  /// never been opened and costs no request.
  final _loaded = <RequestsTab>{};

  /// Bumped whenever a tab's first page is (re)loaded, so a slower response
  /// to an older search can't overwrite a newer one.
  final _generation = {for (final tab in RequestsTab.values) tab: 0};

  RequestsListCubit(this._getTriagedRequests,
      this._confirmRequest,
      this._deleteRequest, {
        Duration searchDelay = const Duration(milliseconds: 400),
      })
      : _searchDelay = searchDelay,
        super(const RequestsListState());

  /// Loads the tab being shown. Safe to call more than once.
  Future<void> start() async {
    if (_started) return;
    _started = true;
    await _loadTab(state.tab);
  }

  /// Shows [tab], fetching its first page the first time it is opened.
  void setTab(RequestsTab tab) {
    if (tab == state.tab) return;
    emit(state.copyWith(tab: tab));
    _loadTab(tab);
  }

  /// Searches for [text], once typing pauses. Only the tab being shown is
  /// fetched; the other loads again when it is opened.
  void setQuery(String text) {
    _searchTimer?.cancel();
    final query = text.trim();
    if (query == state.query) return;
    _searchTimer = Timer(_searchDelay, () {
      emit(state.copyWith(query: query));
      _reloadShown();
    });
  }

  /// Reloads the tab being shown, from its error screen.
  Future<void> retry() => _loadFirstPage(state.tab);

  /// Pull-to-refresh: reloads the tab being shown in place, keeping what is
  /// on screen (and on a failure).
  Future<void> refresh() => _reloadShown(showLoading: false);

  /// Fetches the next page of [tab] (the one being shown by default).
  Future<void> loadMore([RequestsTab? tab]) async {
    final which = tab ?? state.tab;
    final current = state.tabState(which);
    if (!current.canLoadMore) return;
    final generation = _generation[which];
    emit(state.withTab(which, current.copyWith(loadingMore: true)));
    try {
      final page = await _getTriagedRequests(
        which.triage,
        query: state.query,
        page: current.requests.nextPage,
      );
      if (isClosed || generation != _generation[which]) return;
      final now = state.tabState(which);
      emit(
        state.withTab(
          which,
          now.copyWith(
            requests: now.requests.merge(page, keyOf: (r) => r.id),
            loadingMore: false,
          ),
        ),
      );
    } catch (_) {
      if (isClosed || generation != _generation[which]) return;
      emit(
        state.withTab(
          which,
          state.tabState(which).copyWith(
              loadingMore: false, loadMoreFailed: true),
        ),
      );
    }
  }

  /// Keeps a "maybe" email as a request: it leaves Maybe and joins New.
  /// Returns whether it worked.
  Future<bool> confirm(String requestId) async {
    if (!_begin(requestId)) return false;
    try {
      final confirmed = await _confirmRequest(requestId);
      if (isClosed) return true;
      emit(
        state.copyWith(
          // A New tab that was never opened isn't touched: it fetches the
          // request along with the rest when it is.
          newTab: _loaded.contains(RequestsTab.newTab)
              ? state.newTab.copyWith(
                  requests: state.newTab.requests.mapItems(
                    (items) => [
                      confirmed,
                      ...items.where((r) => r.id != requestId),
                    ]..sort((a, b) => b.receivedAt.compareTo(a.receivedAt)),
                  ),
                )
              : state.newTab,
          maybeTab: _without(state.maybeTab, requestId),
          busy: _finish(requestId),
        ),
      );
      return true;
    } catch (_) {
      if (!isClosed) emit(state.copyWith(busy: _finish(requestId)));
      return false;
    }
  }

  /// Discards a "maybe" email. Returns whether it worked.
  Future<bool> delete(String requestId) async {
    if (!_begin(requestId)) return false;
    try {
      await _deleteRequest(requestId);
      if (isClosed) return true;
      emit(
        state.copyWith(
          maybeTab: _without(state.maybeTab, requestId),
          busy: _finish(requestId),
        ),
      );
      return true;
    } catch (_) {
      if (!isClosed) emit(state.copyWith(busy: _finish(requestId)));
      return false;
    }
  }

  bool _begin(String requestId) {
    if (state.busy.contains(requestId)) return false;
    emit(state.copyWith(busy: {...state.busy, requestId}));
    return true;
  }

  Set<String> _finish(String requestId) => {...state.busy}..remove(requestId);

  RequestsTabState _without(RequestsTabState tab, String requestId) =>
      tab.copyWith(
        requests: tab.requests.mapItems(
              (items) =>
          [
            for (final r in items)
              if (r.id != requestId) r,
          ],
        ),
      );

  /// Fetches [tab]'s first page unless it was already fetched.
  Future<void> _loadTab(RequestsTab tab) async {
    if (!_loaded.add(tab)) return;
    await _loadFirstPage(tab);
  }

  /// Reloads the tab being shown. The others are reset instead of fetched:
  /// they load again when opened, so nothing is requested that isn't on
  /// screen.
  Future<void> _reloadShown({bool showLoading = true}) {
    final shown = state.tab;
    for (final tab in RequestsTab.values) {
      if (tab == shown) continue;
      _generation[tab] = _generation[tab]! + 1; // drop a response in flight
      _loaded.remove(tab);
      emit(state.withTab(tab, const RequestsTabState()));
    }
    _loaded.add(shown);
    return _loadFirstPage(shown, showLoading: showLoading);
  }

  /// Replaces [tab]'s list with its first page. [showLoading] swaps the list
  /// for the skeleton meanwhile; without it the old rows stay until the new
  /// ones arrive, and a failure leaves them.
  Future<void> _loadFirstPage(RequestsTab tab,
      {bool showLoading = true}) async {
    final generation = _generation[tab]! + 1;
    _generation[tab] = generation;
    if (showLoading) emit(state.withTab(tab, const RequestsTabState()));
    try {
      final page = await _getTriagedRequests(tab.triage, query: state.query);
      if (isClosed || generation != _generation[tab]) return;
      emit(
        state.withTab(
          tab,
          RequestsTabState(status: RequestsListStatus.ready, requests: page),
        ),
      );
    } catch (error) {
      if (isClosed || generation != _generation[tab]) return;
      if (!showLoading && state
          .tabState(tab)
          .status == RequestsListStatus.ready) {
        return;
      }
      emit(
        state.withTab(
          tab,
          RequestsTabState(
            status: RequestsListStatus.error,
            failure: error is RequestsFailure
                ? error.code
                : RequestsFailureCode.unknown,
          ),
        ),
      );
    }
  }

  @override
  Future<void> close() {
    _searchTimer?.cancel();
    return super.close();
  }
}
