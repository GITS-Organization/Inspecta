import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../machines/domain/entities/delivery.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../../machines/domain/entities/machine_status.dart';
import '../../../machines/domain/usecases/get_job_machines.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';

part 'send_certificate_state.dart';

/// Backs the coordinator's job screen for an approved job (Feature 04, step
/// 3): the job's certificates from the machines API and, once it has been
/// sent, what went out to the client. The email itself is edited and sent on
/// its own screen, which `DeliveryEmailCubit` backs.
///
/// "Preview email" is on offer only while the API says `canDeliver`.
class SendCertificateCubit extends Cubit<SendCertificateState> {
  final InspectionRequest request;
  final GetJobMachines _getMachines;

  SendCertificateCubit({required this.request, required GetJobMachines getJobMachines})
    : _getMachines = getJobMachines,
      super(const SendCertificateState());

  /// Loads the job — also the reload after the email was sent. A failed
  /// reload keeps what is on screen; only a first load shows the error.
  Future<void> start() async {
    try {
      final job = await _getMachines(request.id);
      if (isClosed) return;
      emit(state.copyWith(status: SendCertificateStatus.ready, job: job));
    } catch (error) {
      if (isClosed || state.status == SendCertificateStatus.ready) return;
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
}
