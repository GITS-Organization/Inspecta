import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/entities/certificate_failure.dart';
import '../../../certificate/domain/machine_certificate_mapper.dart';
import '../../../machines/domain/usecases/approve_machine.dart';
import '../../../machines/domain/usecases/get_machine_form.dart';
import '../../../machines/domain/usecases/return_machine.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';

part 'review_detail_state.dart';

/// Backs one machine's review screen (Feature 06): the inspector's submitted
/// certificate for the machine, read from the machines API, plus the
/// reviewer's own signature — approved with it (the API issues the number and
/// QR code and hands the machine on to the coordinator), or returned with a
/// reason.
///
/// The reviewer's name is their profile name.
class ReviewDetailCubit extends Cubit<ReviewDetailState> {
  final InspectionRequest request;
  final String requestItemId;
  final GetMachineForm _getMachineForm;
  final ApproveMachine _approve;
  final ReturnMachine _return;

  bool _started = false;

  ReviewDetailCubit({
    required this.request,
    required this.requestItemId,
    required String reviewerName,
    required GetMachineForm getMachineForm,
    required ApproveMachine approveMachine,
    required ReturnMachine returnMachine,
  }) : _getMachineForm = getMachineForm,
       _approve = approveMachine,
       _return = returnMachine,
       super(ReviewDetailState(reviewerName: reviewerName));

  void start() {
    if (_started) return;
    _started = true;
    _load();
  }

  Future<void> retry() {
    emit(state.copyWith(status: ReviewDetailStatus.loading));
    return _load();
  }

  Future<void> _load() async {
    try {
      final form = await _getMachineForm(request.id, requestItemId);
      if (isClosed) return;
      emit(
        state.copyWith(
          status: ReviewDetailStatus.ready,
          certificate: MachineCertificateMapper.toCertificate(form),
          canReview: form.canReview,
        ),
      );
    } catch (_) {
      if (isClosed) return;
      emit(state.copyWith(status: ReviewDetailStatus.error));
    }
  }

  Future<void> approve() async {
    if (!state.canApprove || state.isBusy) return;
    emit(state.copyWith(isApproving: true));
    try {
      await _approve(request.id, requestItemId);
      _finish(approved: true);
    } catch (e) {
      _fail(e);
    }
  }

  Future<void> sendBack(String note) async {
    if (state.certificate == null || !state.canReview || state.isBusy) return;
    emit(state.copyWith(isReturning: true));
    try {
      await _return(request.id, requestItemId, note: note.trim());
      _finish(approved: false);
    } catch (e) {
      _fail(e);
    }
  }

  void _finish({required bool approved}) {
    if (isClosed) return;
    emit(
      state.copyWith(
        isApproving: false,
        isReturning: false,
        actionSeq: state.actionSeq + 1,
        lastActionSuccess: true,
        lastActionApproved: approved,
      ),
    );
  }

  void _fail(Object error) {
    if (isClosed) return;
    emit(
      state.copyWith(
        isApproving: false,
        isReturning: false,
        actionSeq: state.actionSeq + 1,
        lastActionSuccess: false,
        lastActionFailure: _failureOf(error),
      ),
    );
  }

  static CertificateFailureCode _failureOf(Object error) {
    if (error is CertificateFailure) return error.code;
    if (error is RequestsFailure) {
      return switch (error.code) {
        RequestsFailureCode.permissionDenied => CertificateFailureCode.permissionDenied,
        RequestsFailureCode.network => CertificateFailureCode.network,
        RequestsFailureCode.conflict || RequestsFailureCode.unknown => CertificateFailureCode.unknown,
      };
    }
    return CertificateFailureCode.unknown;
  }
}
