import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/enums/job_status.dart';
import '../../../auth/domain/entities/user.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/usecases/watch_certificate.dart';
import '../../../coordinator/domain/usecases/get_inspectors.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/usecases/get_requests.dart';

part 'review_queue_state.dart';

/// Backs the technical manager's Review and Sent tabs (Feature 06): every
/// job whose certificate is waiting for a signature, was sent back, or has
/// gone out to the client.
class ReviewQueueCubit extends Cubit<ReviewQueueState> {
  final GetRequests _getRequests;
  final GetInspectors _getInspectors;
  final WatchCertificate _watchCertificate;

  StreamSubscription<List<InspectionRequest>>? _requestsSubscription;
  StreamSubscription<List<AppUser>>? _inspectorsSubscription;

  /// One per job in the queue whose request doesn't carry its own
  /// submission time, by job id.
  final _certificateSubscriptions = <String, StreamSubscription<Certificate?>>{};

  ReviewQueueCubit(this._getRequests, this._getInspectors, this._watchCertificate)
    : super(const ReviewQueueState());

  void start() {
    if (_requestsSubscription != null) return;
    _requestsSubscription = _getRequests().listen(
      (requests) {
        emit(state.copyWith(status: ReviewQueueStatus.ready, requests: requests, clearFailure: true));
        _trackLegacySubmissions(requests);
      },
      onError: (Object error) => emit(
        state.copyWith(
          status: ReviewQueueStatus.error,
          failure: error is RequestsFailure ? error.code : RequestsFailureCode.unknown,
        ),
      ),
    );
    // Names are a nicety for the cards — if this read is refused the
    // queue still works, it just can't say who the inspector was.
    _inspectorsSubscription = _getInspectors().listen(
      (inspectors) => emit(state.copyWith(inspectorNames: {for (final i in inspectors) i.id: i.name})),
      onError: (_) {},
    );
  }

  /// Jobs submitted before the request recorded when (`certificateSubmittedAt`)
  /// are ordered by their certificate's own submission time instead — read
  /// here, only for the ones waiting on the technical manager or sent back,
  /// and dropped again once they leave the queue. A certificate that can't
  /// be read just keeps the scheduled-time order.
  void _trackLegacySubmissions(List<InspectionRequest> requests) {
    final needed = {
      for (final request in requests)
        if ((request.status == JobStatus.certificateSubmitted || request.status == JobStatus.certificateReturned) &&
            request.certificateSubmittedAt == null)
          request.id,
    };
    for (final id in _certificateSubscriptions.keys.toList()) {
      if (!needed.contains(id)) _certificateSubscriptions.remove(id)?.cancel();
    }
    for (final id in needed) {
      _certificateSubscriptions.putIfAbsent(
        id,
        () => _watchCertificate(id).listen((certificate) {
          final submittedAt = certificate?.submittedAt;
          if (submittedAt == null || state.legacySubmittedAt[id] == submittedAt) return;
          emit(state.copyWith(legacySubmittedAt: {...state.legacySubmittedAt, id: submittedAt}));
        }, onError: (_) {}),
      );
    }
  }

  Future<void> retry() async {
    await _requestsSubscription?.cancel();
    await _inspectorsSubscription?.cancel();
    _requestsSubscription = null;
    _inspectorsSubscription = null;
    emit(state.copyWith(status: ReviewQueueStatus.loading, clearFailure: true));
    start();
  }

  @override
  Future<void> close() async {
    await _requestsSubscription?.cancel();
    await _inspectorsSubscription?.cancel();
    for (final subscription in _certificateSubscriptions.values) {
      await subscription.cancel();
    }
    return super.close();
  }
}
