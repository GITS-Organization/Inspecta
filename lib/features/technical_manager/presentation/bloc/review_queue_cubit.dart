import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/enums/job_status.dart';
import '../../../queue/domain/usecases/get_queue.dart';
import '../../../queue/domain/usecases/get_to_review.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';

part 'review_queue_state.dart';

/// Backs the technical manager's Review and Sent tabs (Feature 06): the jobs
/// the API lists under `GET /quotation-queue?status=certify` — certificates
/// sent back or gone out to the client — and, for what waits on a signature,
/// `GET /quotation-queue/to-review`, whose rows carry certificate counts.
class ReviewQueueCubit extends Cubit<ReviewQueueState> {
  /// The queue list a technical manager works from.
  static const queueStatus = 'certify';

  final GetQueue _getQueue;
  final GetToReview _getToReview;

  /// Bumped per fetch so a slow, superseded response can't overwrite a newer
  /// one.
  int _fetch = 0;

  ReviewQueueCubit(this._getQueue, this._getToReview) : super(const ReviewQueueState());

  /// Loads the queue — also the reload after returning from a screen that may
  /// have moved a job on. A failed reload keeps the list already on screen;
  /// only a first load shows the error.
  Future<void> start() async {
    final fetch = ++_fetch;
    try {
      final requests = await _getQueue(status: queueStatus);
      final toReview = await _getToReview();
      if (isClosed || fetch != _fetch) return;
      emit(
        state.copyWith(
          status: ReviewQueueStatus.ready,
          requests: requests,
          toReviewRequests: toReview,
          clearFailure: true,
        ),
      );
    } catch (error) {
      if (isClosed || fetch != _fetch) return;
      if (state.status == ReviewQueueStatus.ready) return;
      emit(
        state.copyWith(
          status: ReviewQueueStatus.error,
          failure: error is RequestsFailure ? error.code : RequestsFailureCode.unknown,
        ),
      );
    }
  }

  Future<void> retry() {
    emit(state.copyWith(status: ReviewQueueStatus.loading, clearFailure: true));
    return start();
  }
}
