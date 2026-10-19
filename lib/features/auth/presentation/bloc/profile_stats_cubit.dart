import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/enums/job_status.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/usecases/watch_reviewed_certificates.dart';
import '../../../clients/domain/entities/client.dart';
import '../../../clients/domain/usecases/get_clients.dart';
import '../../../quotation/domain/entities/quotation.dart';
import '../../../quotation/domain/entities/quotation_status.dart';
import '../../../quotation/domain/usecases/get_quotations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/usecases/get_requests.dart';
import '../../domain/entities/user_role.dart';

part 'profile_stats_state.dart';

/// Backs the "This month" stat tiles on the Profile screen: a coordinator's
/// quotes sent / accepted and client count, and a technical manager's
/// certificates reviewed / returned / sent. The figures are the whole team's,
/// not just the signed-in user's. An inspector has no data source for their
/// tiles yet (their feature isn't built), so their counts simply stay null
/// forever — the Profile screen renders a placeholder for those.
class ProfileStatsCubit extends Cubit<ProfileStatsState> {
  final GetQuotations _getQuotations;
  final GetClients _getClients;
  final GetRequests _getRequests;
  final WatchReviewedCertificates _watchReviewedCertificates;

  final _subscriptions = <StreamSubscription<Object?>>[];
  bool _started = false;

  List<InspectionRequest>? _requests;
  List<Certificate>? _reviewedCertificates;

  ProfileStatsCubit(this._getQuotations, this._getClients, this._getRequests, this._watchReviewedCertificates)
    : super(const ProfileStatsState());

  /// Subscribes to what [role]'s tiles are counted from.
  void start(UserRole? role) {
    if (_started) return;
    _started = true;
    switch (role) {
      case UserRole.coordinator:
        _startCoordinatorStats();
      case UserRole.technicalManager:
        _startReviewStats();
      case UserRole.inspector || UserRole.admin || null:
        break;
    }
  }

  void _startCoordinatorStats() {
    _subscriptions.add(
      _getQuotations().listen((quotations) {
        final now = DateTime.now();
        bool inThisMonth(DateTime? at) => at != null && at.year == now.year && at.month == now.month;
        emit(
          state.copyWith(
            quotesSentThisMonth: quotations.where((q) => inThisMonth(q.sentAt)).length,
            acceptedThisMonth: quotations
                .where((q) => q.status == QuotationStatus.accepted && inThisMonth(q.clientRespondedAt))
                .length,
          ),
        );
      }, onError: (_) {}),
    );

    _subscriptions.add(
      _getClients().listen((clients) => emit(state.copyWith(clientsCount: clients.length)), onError: (_) {}),
    );
  }

  /// A certificate's review time is stamped both when it's approved and when
  /// it's sent back, so those reviewed this month are told apart by where the
  /// job is now: sent to the client, or back with the inspector (still
  /// returned, or resubmitted since). One that was returned and has since
  /// been approved counts as sent, since its review time is now the approval.
  void _startReviewStats() {
    final now = DateTime.now();
    _subscriptions.add(
      _getRequests().listen((requests) {
        _requests = requests;
        _countReviews();
      }, onError: (_) {}),
    );
    _subscriptions.add(
      _watchReviewedCertificates(DateTime(now.year, now.month)).listen((certificates) {
        _reviewedCertificates = certificates;
        _countReviews();
      }, onError: (_) {}),
    );
  }

  void _countReviews() {
    final requests = _requests;
    final certificates = _reviewedCertificates;
    if (requests == null || certificates == null) return;

    final statusOf = {for (final request in requests) request.id: request.status};
    var sent = 0;
    var returned = 0;
    for (final certificate in certificates) {
      switch (statusOf[certificate.requestId]) {
        case JobStatus.sentToClient:
          sent++;
        case JobStatus.certificateReturned || JobStatus.certificateSubmitted:
          returned++;
        default:
          break;
      }
    }
    emit(state.copyWith(reviewedThisMonth: sent + returned, returnedThisMonth: returned, sentThisMonth: sent));
  }

  @override
  Future<void> close() async {
    for (final subscription in _subscriptions) {
      await subscription.cancel();
    }
    return super.close();
  }
}
