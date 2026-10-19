import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../domain/entities/quotation.dart';
import '../../domain/entities/quotation_failure.dart';
import '../../domain/entities/quotation_status.dart';
import '../../domain/entities/quote_board_tab.dart';
import '../../domain/usecases/get_quotations.dart';

part 'quotations_list_state.dart';

/// Backs the Quotations tab: the coordinator's price tracker. Groups
/// every quotation by request and keeps only each request's current one
/// — drafts and superseded versions don't get their own row on the
/// board (a draft is still visible on the Requests tab; superseded
/// versions live in that request's quote detail timeline instead).
class QuotationsListCubit extends Cubit<QuotationsListState> {
  final GetQuotations _getQuotations;

  StreamSubscription<List<Quotation>>? _subscription;

  QuotationsListCubit(this._getQuotations) : super(const QuotationsListState());

  void start() {
    if (_subscription != null) return;
    _subscription = _getQuotations().listen(
      (quotations) => emit(
        state.copyWith(status: QuotationsListStatus.ready, board: _currentPerRequest(quotations)),
      ),
      onError: (Object error) => emit(
        state.copyWith(
          status: QuotationsListStatus.error,
          failure: error is QuotationFailure ? error.code : QuotationFailureCode.unknown,
        ),
      ),
    );
  }

  List<Quotation> _currentPerRequest(List<Quotation> all) {
    final byRequest = <String, Quotation>{};
    for (final q in all) {
      if (q.status == QuotationStatus.draft || q.status == QuotationStatus.superseded) {
        continue;
      }
      final existing = byRequest[q.requestId];
      if (existing == null || q.createdAt.isAfter(existing.createdAt)) {
        byRequest[q.requestId] = q;
      }
    }
    return byRequest.values.toList();
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

  Future<void> retry() async {
    await _subscription?.cancel();
    _subscription = null;
    emit(state.copyWith(status: QuotationsListStatus.loading));
    start();
  }

  @override
  Future<void> close() async {
    await _subscription?.cancel();
    return super.close();
  }
}
