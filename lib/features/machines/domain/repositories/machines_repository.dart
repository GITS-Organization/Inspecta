import 'dart:typed_data';

import '../entities/delivery.dart';
import '../entities/job_machines.dart';
import '../entities/machine_form.dart';

/// Methods throw [RequestsFailure] on expected errors.
abstract interface class MachinesRepository {
  /// The machines to inspect on a job, with their certificate progress.
  Future<JobMachines> getMachines(String requestId);

  /// One machine's whole form in one call.
  Future<MachineForm> getMachineForm(String requestId, String requestItemId);

  /// Saves what the inspector entered (a partial update) and returns the
  /// form as the API now holds it.
  Future<MachineForm> saveMachineForm(
    String requestId,
    String requestItemId,
    MachineFormChanges changes,
  );

  /// Submits the machine's certificate to the technical manager. Throws a
  /// [CertificateIncompleteFailure] listing what is still missing.
  Future<void> submitMachine(String requestId, String requestItemId);

  /// The technical manager approves the machine's certificate; the API signs
  /// it with the signature it holds for them.
  Future<void> approveMachine(String requestId, String requestItemId);

  /// The technical manager sends the machine's certificate back to its
  /// inspector, with the reason in [note].
  Future<void> returnMachine(String requestId, String requestItemId, {required String note});

  /// The technical manager approves every certificate of the job at once —
  /// all or nothing. [signature] is the drawn strokes (flat, 0..1-normalised
  /// `[x, y, ...]` lists), never an image; left out, the API signs with the
  /// one it holds. Throws a [CertificateIncompleteFailure] listing the
  /// machines that have no submitted certificate.
  Future<void> approveAll(
    String requestId, {
    List<List<double>>? signature,
    bool rememberSignature = false,
  });

  /// The technical manager sends the whole job back to its inspector with
  /// [note], and optionally a comment per machine ([machineComments], by
  /// request item id).
  Future<void> returnToInspector(
    String requestId, {
    required String note,
    Map<String, String> machineComments = const {},
  });

  /// The email that would go to the client with the job's certificates.
  Future<DeliveryPreview> getDeliveryPreview(String requestId);

  /// The coordinator hands the job's certificates to the client: the job
  /// becomes sent to client. [sentTo] and [message] are optional.
  Future<DeliveryResult> deliverToClient(String requestId, {String? sentTo, String? message});

  /// The machine's certificate PDF as the API renders it: marked DRAFT until
  /// approved, then the stored final file with its QR code.
  Future<Uint8List> getMachinePdf(String requestId, String requestItemId);

  /// Begins the inspection: accepts the task if needed and moves the job to
  /// in progress. Safe to call again.
  Future<void> startInspection(String requestId);
}
