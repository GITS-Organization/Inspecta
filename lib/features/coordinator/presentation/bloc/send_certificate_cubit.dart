import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/utils/validators.dart';
import '../../../certificate/domain/entities/certificate_failure.dart';
import '../../../machines/domain/entities/delivery.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../../machines/domain/entities/machine_status.dart';
import '../../../machines/domain/usecases/deliver_to_client.dart';
import '../../../machines/domain/usecases/get_delivery_preview.dart';
import '../../../machines/domain/usecases/get_job_machines.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';

part 'send_certificate_state.dart';

/// Backs the coordinator's "Send certificate" screen (Feature 04, step 3 and
/// 4): the job's approved certificates from the machines API, the email that
/// would go to the client, and the one action that hands the job over.
///
/// "Deliver" is on offer only while the API says `canDeliver`.
class SendCertificateCubit extends Cubit<SendCertificateState> {
  final InspectionRequest request;
  final GetJobMachines _getMachines;
  final GetDeliveryPreview _getPreview;
  final DeliverToClient _deliver;

  SendCertificateCubit({
    required this.request,
    required GetJobMachines getJobMachines,
    required GetDeliveryPreview getDeliveryPreview,
    required DeliverToClient deliverToClient,
  }) : _getMachines = getJobMachines,
       _getPreview = getDeliveryPreview,
       _deliver = deliverToClient,
       super(const SendCertificateState());

  Future<void> start() async {
    try {
      final job = await _getMachines(request.id);
      // The email preview is a courtesy: a job that can't be delivered (or a
      // preview that fails) still shows its certificates.
      DeliveryPreview? preview;
      if (job.canDeliver) {
        try {
          preview = await _getPreview(request.id);
        } catch (_) {}
      }
      if (isClosed) return;
      emit(
        state.copyWith(
          status: SendCertificateStatus.ready,
          job: job,
          preview: preview,
          email: state.email.isEmpty ? preview?.to : null,
          suggestedEmail: preview?.to,
        ),
      );
    } catch (error) {
      if (isClosed) return;
      emit(
        state.copyWith(
          status: SendCertificateStatus.error,
          loadFailure: error is RequestsFailure ? error.code : RequestsFailureCode.unknown,
        ),
      );
    }
  }

  Future<void> retry() {
    emit(state.copyWith(status: SendCertificateStatus.loading));
    return start();
  }

  void setEmail(String value) => emit(state.copyWith(email: value));

  void setMessage(String value) => emit(state.copyWith(message: value));

  Future<void> send() async {
    if (!state.canSend) return;
    emit(state.copyWith(isSending: true));
    try {
      final result = await _deliver(
        request.id,
        sentTo: state.email.trim(),
        message: state.message.trim(),
      );
      if (isClosed) return;
      emit(
        state.copyWith(
          isSending: false,
          actionSeq: state.actionSeq + 1,
          lastActionSuccess: true,
          lastResult: result,
        ),
      );
    } catch (e) {
      if (isClosed) return;
      emit(
        state.copyWith(
          isSending: false,
          actionSeq: state.actionSeq + 1,
          lastActionSuccess: false,
          lastActionFailure: _failureOf(e),
        ),
      );
    }
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
