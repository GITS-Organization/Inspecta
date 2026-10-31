import '../../machines/domain/usecases/get_machine_form.dart';
import '../../machines/domain/usecases/save_machine_form.dart';
import '../../machines/domain/usecases/submit_machine.dart';

/// Where the certificate form reads, saves and submits when it is open on one
/// machine of a job through the machines API, instead of on the job's
/// Firestore certificate.
class MachineCertificateSource {
  final String requestItemId;
  final GetMachineForm getForm;
  final SaveMachineForm saveForm;
  final SubmitMachine submit;

  const MachineCertificateSource({
    required this.requestItemId,
    required this.getForm,
    required this.saveForm,
    required this.submit,
  });
}
