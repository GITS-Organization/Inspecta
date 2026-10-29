import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../auth/domain/entities/user.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/domain/usecases/assign_inspector.dart';
import '../../domain/entities/coordinator_failure.dart';
import '../../domain/usecases/get_inspectors.dart';

part 'assign_inspector_state.dart';

/// Feature 04 §5's "Assign inspector" screen: picks a time slot and an
/// inspector for one ready-to-assign job, then submits the assignment.
class AssignInspectorCubit extends Cubit<AssignInspectorState> {
  final InspectionRequest request;

  /// Set when arriving from an inspector's own detail screen ("+ Assign
  /// a job"), so that inspector is pre-selected instead of the best
  /// match.
  final String? preselectedInspectorId;

  final GetInspectors _getInspectors;
  final AssignInspector _assignInspector;

  StreamSubscription<List<AppUser>>? _subscription;

  AssignInspectorCubit({
    required this.request,
    this.preselectedInspectorId,
    required GetInspectors getInspectors,
    required AssignInspector assignInspector,
  }) : _getInspectors = getInspectors,
       _assignInspector = assignInspector,
       super(AssignInspectorState(scheduledAt: _defaultScheduledAt(request)));

  static DateTime _defaultScheduledAt(InspectionRequest request) {
    final base = request.preferredDate ?? DateTime.now().add(const Duration(days: 1));
    return DateTime(base.year, base.month, base.day, 9, 30);
  }

  bool get canSubmit => state.selectedInspectorId != null && !state.isSubmitting;

  void start() {
    if (_subscription != null) return;
    _subscription = _getInspectors().listen(
      (inspectors) {
        final sorted = _sortedByMatch(inspectors);
        // Pre-select the best match, like the mockup's radio default — an
        // inspector on leave can't be assigned, so they're never the
        // default even if they'd otherwise sort first.
        final available = sorted.where((i) => !i.isOnLeave());
        final preselected = preselectedInspectorId;
        final defaultId = (preselected != null && available.any((i) => i.id == preselected))
            ? preselected
            : (available.isNotEmpty ? available.first.id : (sorted.isEmpty ? null : sorted.first.id));
        emit(
          state.copyWith(
            inspectorsStatus: InspectorsStatus.ready,
            inspectors: sorted,
            matchedInspectorIds: _matchedIds(inspectors),
            selectedInspectorId: state.selectedInspectorId ?? defaultId,
          ),
        );
      },
      onError: (Object error) => emit(
        state.copyWith(
          inspectorsStatus: InspectorsStatus.error,
          inspectorsFailure: _mapFailure(error),
        ),
      ),
    );
  }

  void selectInspector(String inspectorId) =>
      emit(state.copyWith(selectedInspectorId: inspectorId));

  void changeDate(DateTime date) {
    final current = state.scheduledAt;
    emit(
      state.copyWith(
        scheduledAt: DateTime(date.year, date.month, date.day, current.hour, current.minute),
      ),
    );
  }

  void changeTime(int hour, int minute) {
    final current = state.scheduledAt;
    emit(
      state.copyWith(
        scheduledAt: DateTime(current.year, current.month, current.day, hour, minute),
      ),
    );
  }

  void changeNote(String note) => emit(state.copyWith(note: note));

  Future<void> submit() async {
    if (!state.submitted) emit(state.copyWith(submitted: true));
    if (!canSubmit) return;

    emit(state.copyWith(isSubmitting: true));
    try {
      await _assignInspector(
        requestId: request.id,
        inspectorId: state.selectedInspectorId!,
        scheduledAt: state.scheduledAt,
        note: state.note.trim().isEmpty ? null : state.note.trim(),
      );
      _complete(success: true);
    } catch (e) {
      _complete(success: false, failure: _mapFailure(e));
    }
  }

  /// Equipment types on [request] the inspector is qualified for, so the
  /// mockup's "Best match" tag reflects a real overlap rather than a
  /// guess — inspectors with no overlap just sort after the matches.
  Set<String> _neededTypes() => request.items
      .map((item) => item.type.toLowerCase().trim())
      .where((type) => type.isNotEmpty)
      .toSet();

  bool _isQualified(AppUser inspector, Set<String> neededTypes) => inspector.qualifications.any(
    (q) => neededTypes.any(
      (type) => type.contains(q.name.toLowerCase()) || q.name.toLowerCase().contains(type),
    ),
  );

  List<AppUser> _sortedByMatch(List<AppUser> inspectors) {
    final needed = _neededTypes();
    final matched = <AppUser>[];
    final unmatched = <AppUser>[];
    for (final inspector in inspectors) {
      (_isQualified(inspector, needed) ? matched : unmatched).add(inspector);
    }
    return [...matched, ...unmatched];
  }

  Set<String> _matchedIds(List<AppUser> inspectors) {
    final needed = _neededTypes();
    return inspectors.where((i) => _isQualified(i, needed)).map((i) => i.id).toSet();
  }

  CoordinatorFailureCode _mapFailure(Object error) {
    if (error is CoordinatorFailure) return error.code;
    if (error is RequestsFailure) {
      return switch (error.code) {
        RequestsFailureCode.permissionDenied => CoordinatorFailureCode.permissionDenied,
        RequestsFailureCode.network => CoordinatorFailureCode.network,
        RequestsFailureCode.conflict || RequestsFailureCode.unknown => CoordinatorFailureCode.unknown,
      };
    }
    return CoordinatorFailureCode.unknown;
  }

  void _complete({required bool success, CoordinatorFailureCode? failure}) {
    if (isClosed) return;
    emit(
      state.copyWith(
        isSubmitting: false,
        actionSeq: state.actionSeq + 1,
        lastActionSuccess: success,
        lastActionFailure: failure,
        clearFailure: failure == null,
      ),
    );
  }

  @override
  Future<void> close() async {
    await _subscription?.cancel();
    return super.close();
  }
}
