import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/base/paginated.dart';
import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quote_board_tab.dart';
import '../../domain/usecases/get_quotation_board.dart';

part 'quotations_list_state.dart';

/// Backs the Quotations tab: the coordinator's price tracker. Loads the
/// board from `GET /quotations` a page at a time. Drafts are listed under
/// Open; superseded versions aren't requested (they live in that request's
/// quote detail timeline instead).
class QuotationsListCubit extends Cubit<QuotationsListState> {
  /// A page can come back empty before the last one; the first load reads on
  /// through that many pages rather than show an empty board.
  static const _maxEmptyPages = 5;

  final GetQuotationBoard _getBoard;

  bool _started = false;

  /// Bumped on every reload, so a slow older response can't overwrite a
  /// newer one.
  int _generation = 0;

  QuotationsListCubit(this._getBoard) : super(const QuotationsListState());

  Future<void> start() async {
    if (_started) return;
    _started = true;
    await _loadFirstPage();
  }

  void setTab(QuoteBoardTab tab) {
    if (tab != state.tab || state.onlyExpiringSoon) {
      emit(state.copyWith(tab: tab, onlyExpiringSoon: false));
    }
  }

  void showExpiringSoon() => emit(state.copyWith(onlyExpiringSoon: true));

  void setQuery(String query) {
    if (query != state.query) emit(state.copyWith(query: query));
  }

  /// Reloads from the error screen.
  Future<void> retry() {
    emit(state.copyWith(status: QuotationsListStatus.loading, clearFailure: true));
    return _loadFirstPage();
  }

  /// Pull-to-refresh: reloads in place, keeping the board on screen (and on
  /// a failure).
  Future<void> refresh() => _loadFirstPage(keepOnError: true);

  /// Fetches the next page of requests' quotes.
  Future<void> loadMore() async {
    if (!state.canLoadMore) return;
    final generation = _generation;
    emit(state.copyWith(loadingMore: true));
    try {
      final next = await _getBoard(page: state.page.nextPage);
      if (isClosed || generation != _generation) return;
      emit(
        state.copyWith(
          page: state.page.merge(next, keyOf: (q) => q.id),
          loadingMore: false,
        ),
      );
    } catch (_) {
      if (isClosed || generation != _generation) return;
      emit(state.copyWith(loadingMore: false, loadMoreFailed: true));
    }
  }

  Future<void> _loadFirstPage({bool keepOnError = false}) async {
    final generation = ++_generation;
    try {
      var board = await _getBoard();
      for (var i = 0; i < _maxEmptyPages && board.isEmpty && board.hasMore; i++) {
        board = board.merge(await _getBoard(page: board.nextPage));
      }
      if (isClosed || generation != _generation) return;
      emit(
        QuotationsListState(
          status: QuotationsListStatus.ready,
          page: board,
          tab: state.tab,
          query: state.query,
          onlyExpiringSoon: state.onlyExpiringSoon,
        ),
      );
    } catch (error) {
      if (isClosed || generation != _generation) return;
      if (keepOnError && state.status == QuotationsListStatus.ready) return;
      emit(
        state.copyWith(
          status: QuotationsListStatus.error,
          failure: error is QuotationFailure ? error.code : QuotationFailureCode.unknown,
        ),
      );
    }
  }
}
