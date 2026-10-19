import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/enums/job_status.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/usecases/watch_inspector_certificates.dart';
import '../../../certificate/domain/usecases/watch_reviewed_certificates.dart';
import '../../../clients/domain/entities/client.dart';
import '../../../clients/domain/usecases/get_clients.dart';
import '../../../quotation/domain/entities/quotation.dart';
import '../../../quotation/domain/entities/quotation_status.dart';
import '../../../quotation/domain/usecases/get_quotations.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/usecases/get_my_tasks.dart';
import '../../../requests/domain/usecases/get_requests.dart';
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
  final GetClients _getClients;
  final GetRequests _getRequests;
  final WatchReviewedCertificates _watchReviewedCertificates;
  final GetMyTasks _getMyTasks;
  final WatchInspectorCertificates _watchInspectorCertificates;

  final _subscriptions = <StreamSubscription<Object?>>[];
  bool _started = false;

  List<InspectionRequest>? _requests;
  List<Certificate>? _certificates;

  ProfileStatsCubit(
    this._getQuotations,
    this._getClients,
    this._getRequests,
    this._watchReviewedCertificates,
    this._getMyTasks,
    this._watchInspectorCertificates,
  ) : super(const ProfileStatsState());

  /// Subscribes to what [user]'s role's tiles are counted from.
  void start(AppUser? user) {
    if (_started || user == null) return;
    _started = true;
    switch (user.role) {
      case UserRole.coordinator:
        _startCoordinatorStats();
      case UserRole.technicalManager:
        _startReviewStats();
      case UserRole.inspector:
        _startInspectorStats(user.id);
      case UserRole.admin:
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
        _certificates = certificates;
        _countReviews();
      }, onError: (_) {}),
    );
  }

  void _countReviews() {
    final requests = _requests;
    final certificates = _certificates;
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

  void _startInspectorStats(String inspectorId) {
    _subscriptions.add(
      _getMyTasks(inspectorId).listen((requests) {
        _requests = requests;
        _countInspectorWork();
      }, onError: (_) {}),
    );
    _subscriptions.add(
      _watchInspectorCertificates(inspectorId).listen((certificates) {
        _certificates = certificates;
        _countInspectorWork();
      }, onError: (_) {}),
    );
  }

  /// Inspections: jobs scheduled this month that have been started — under
  /// way, or already certified. Certificates: those handed in this month.
  /// Returned: those a technical manager sent back this month — a returned
  /// certificate keeps its review time, so, as for the manager, it counts
  /// while the job is still returned or has been resubmitted.
  void _countInspectorWork() {
    final now = DateTime.now();
    final requests = _requests;
    final certificates = _certificates;

    var next = state;
    if (requests != null) {
      const started = {
        JobStatus.inProgress,
        JobStatus.certificateSubmitted,
        JobStatus.certificateReturned,
        JobStatus.sentToClient,
      };
      next = next.copyWith(
        inspectionsThisMonth: requests.where((r) => started.contains(r.status) && _inMonth(r.scheduledAt, now)).length,
      );
    }
    if (certificates != null) {
      next = next.copyWith(certificatesThisMonth: certificates.where((c) => _inMonth(c.submittedAt, now)).length);
    }
    if (requests != null && certificates != null) {
      final statusOf = {for (final request in requests) request.id: request.status};
      next = next.copyWith(
        returnedThisMonth: certificates
            .where(
              (c) =>
                  _inMonth(c.reviewedAt, now) &&
                  (statusOf[c.requestId] == JobStatus.certificateReturned ||
                      statusOf[c.requestId] == JobStatus.certificateSubmitted),
            )
            .length,
      );
    }
    emit(next);
  }

  @override
  Future<void> close() async {
    for (final subscription in _subscriptions) {
      await subscription.cancel();
    }
    return super.close();
  }
}
