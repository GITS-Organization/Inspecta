import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/enums/job_status.dart';
import '../../../quotation/domain/entities/quotation.dart';
import '../../../quotation/domain/entities/quotation_status.dart';
import '../../../quotation/domain/usecases/get_quotations.dart';
import '../../../queue/domain/usecases/get_queue.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/entities/user.dart';
import '../../domain/entities/user_role.dart';

part 'profile_stats_state.dart';

/// Backs the "This month" stat tiles on the Profile screen: a coordinator's
/// quotes sent / accepted and client count, a technical manager's
/// certificates reviewed / returned / sent, and an inspector's inspections,
/// certificates and returns. A coordinator's and technical manager's figures
/// are the whole team's; an inspector's are their own.
class ProfileStatsCubit extends Cubit<ProfileStatsState> {
  final GetQuotations _getQuotations;
  final GetQueue _getQueue;

  final _subscriptions = <StreamSubscription<Object?>>[];
  bool _started = false;

  ProfileStatsCubit(this._getQuotations, this._getQueue) : super(const ProfileStatsState());

  /// Subscribes to what [user]'s role's tiles are counted from. A technical
  /// manager's come from [certifyQueue], the shell's own list of the
  /// `certify` queue.
  void start(AppUser? user, {Stream<List<InspectionRequest>>? certifyQueue}) {
    if (_started || user == null) return;
    _started = true;
    switch (user.role) {
      case UserRole.coordinator:
        _startCoordinatorStats();
      case UserRole.technicalManager:
        if (certifyQueue != null) _watchReviewStats(certifyQueue);
      case UserRole.inspector:
        _loadInspectorStats();
      case UserRole.admin:
      case UserRole.finance:
      case UserRole.managingDirector:
      case UserRole.merchandiser:
        break;
    }
  }

  static bool _inMonth(DateTime? at, DateTime now) => at != null && at.year == now.year && at.month == now.month;

  void _startCoordinatorStats() {
    _subscriptions.add(
      _getQuotations().listen((quotations) {
        final now = DateTime.now();
        emit(
          state.copyWith(
            quotesSentThisMonth: quotations.where((q) => _inMonth(q.sentAt, now)).length,
            acceptedThisMonth: quotations
                .where((q) => q.status == QuotationStatus.accepted && _inMonth(q.clientRespondedAt, now))
                .length,
          ),
        );
      }, onError: (_) {}),
    );
  }

  /// Counted from the technical manager's queue (`status=certify`): jobs
  /// approved or sent to the client, and jobs sent back, that were last
  /// changed this month. The shell already fetches that queue, so the counts
  /// follow [certifyQueue] rather than asking the API again.
  void _watchReviewStats(Stream<List<InspectionRequest>> certifyQueue) {
    _subscriptions.add(
      certifyQueue.listen((requests) {
        final now = DateTime.now();
        final inMonth = requests.where((r) => _inMonth(r.updatedAt, now));
        final sent = inMonth
            .where((r) => r.status == JobStatus.certificateApproved || r.status == JobStatus.sentToClient)
            .length;
        final returned = inMonth.where((r) => r.status == JobStatus.certificateReturned).length;
        emit(state.copyWith(reviewedThisMonth: sent + returned, returnedThisMonth: returned, sentThisMonth: sent));
      }, onError: (_) {}),
    );
  }

  /// Counted from the inspector's own jobs in the API's queue.
  ///
  /// Inspections: jobs scheduled this month that have been started — under
  /// way, or already certified. Certificates: jobs handed in (or past that)
  /// and last changed this month. Returned: jobs a technical manager sent
  /// back and that were last changed this month.
  Future<void> _loadInspectorStats() async {
    try {
      final requests = await _getQueue();
      if (isClosed) return;
      final now = DateTime.now();
      const started = {
        JobStatus.inProgress,
        JobStatus.certificateSubmitted,
        JobStatus.certificateReturned,
        JobStatus.certificateApproved,
        JobStatus.sentToClient,
      };
      const handedIn = {
        JobStatus.certificateSubmitted,
        JobStatus.certificateApproved,
        JobStatus.sentToClient,
      };
      emit(
        state.copyWith(
          inspectionsThisMonth: requests.where((r) => started.contains(r.status) && _inMonth(r.scheduledAt, now)).length,
          certificatesThisMonth: requests.where((r) => handedIn.contains(r.status) && _inMonth(r.updatedAt, now)).length,
          returnedThisMonth: requests
              .where((r) => r.status == JobStatus.certificateReturned && _inMonth(r.updatedAt, now))
              .length,
        ),
      );
    } catch (_) {
      // The tiles just stay empty.
    }
  }

  @override
  Future<void> close() async {
    for (final subscription in _subscriptions) {
      await subscription.cancel();
    }
    return super.close();
  }
}
