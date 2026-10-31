import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/enums/job_status.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../queue/domain/usecases/get_queue.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';

part 'certificates_list_state.dart';

/// Backs the inspector's Certificates tab (Feature 05): every job past
/// intake that has (or needs) a certificate, bucketed into Action /
/// Submitted / Sent / All, with a search box over client, equipment and
/// the display cert number. The jobs come from the API's queue, which gives
/// an inspector only their own.
class CertificatesListCubit extends Cubit<CertificatesListState> {
  final String inspectorId;
  final GetQueue _getQueue;

  /// Bumped per fetch so a slow, superseded response can't overwrite a newer
  /// one.
  int _fetch = 0;

  CertificatesListCubit({required this.inspectorId, required GetQueue getQueue})
    : _getQueue = getQueue,
      super(const CertificatesListState());

  /// Loads the jobs — also the pull-to-refresh and the reload on return from
  /// a job. A failed reload keeps the list already on screen; only a first
  /// load shows the error.
  Future<void> start() async {
    final fetch = ++_fetch;
    try {
      final requests = await _getQueue();
      if (isClosed || fetch != _fetch) return;
      emit(state.copyWith(status: CertificatesListStatus.ready, requests: requests, clearFailure: true));
    } catch (error) {
      if (isClosed || fetch != _fetch) return;
      if (state.status == CertificatesListStatus.ready) return;
      emit(
        state.copyWith(
          status: CertificatesListStatus.error,
          failure: error is RequestsFailure ? error.code : RequestsFailureCode.unknown,
        ),
      );
    }
  }

  Future<void> retry() {
    emit(state.copyWith(status: CertificatesListStatus.loading, clearFailure: true));
    return start();
  }

  void setQuery(String query) => emit(state.copyWith(query: query));

  void setFilter(CertificatesFilter filter) => emit(state.copyWith(filter: filter));
}
