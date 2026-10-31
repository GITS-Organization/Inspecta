import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_handler.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../domain/entities/certificate_incomplete_failure.dart';
import '../../domain/entities/delivery.dart';
import '../../domain/entities/job_machines.dart';
import '../../domain/entities/machine_form.dart';
import '../../domain/repositories/machines_repository.dart';
import '../datasources/machines_api_client_services.dart';
import '../models/machine_bodies.dart';
import '../models/machines_parser.dart';

class MachinesRepositoryImpl implements MachinesRepository {
  static const _safeApi = SafeApi();

  final MachinesApiClientServices _api;

  const MachinesRepositoryImpl(this._api);

  @override
  Future<JobMachines> getMachines(String requestId) async {
    final result = await _safeApi(
      () async => MachinesParser.jobMachines(
        await _api.getMachines(requestId) as Map<String, dynamic>,
      ),
    );
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<MachineForm> getMachineForm(String requestId, String requestItemId) async {
    final result = await _safeApi(
      () async => MachinesParser.machineForm(
        await _api.getMachineForm(requestId, requestItemId) as Map<String, dynamic>,
      ),
    );
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<MachineForm> saveMachineForm(
    String requestId,
    String requestItemId,
    MachineFormChanges changes,
  ) async {
    final result = await _safeApi(
      () async => MachinesParser.machineForm(
        await _api.saveMachineForm(requestId, requestItemId, SaveMachineBody(changes).toJson())
            as Map<String, dynamic>,
      ),
    );
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<void> submitMachine(String requestId, String requestItemId) async {
    final result = await _safeApi<void>(
      () => _api.submitMachine(requestId, requestItemId),
      mapError: _incompleteOf,
    );
    result.fold((_) {}, (f) => throw f is CertificateIncompleteFailure ? f : _mapError(f));
  }

  /// The 409 `certificate_incomplete` answer, with its `problems` list.
  static CertificateIncompleteFailure? _incompleteOf(Object error) => _problemsOf(error, 'certificate_incomplete');

  /// The 409 `job_not_ready` answer to an approve-all, with its `problems`
  /// list: a machine has no submitted certificate.
  static CertificateIncompleteFailure? _notReadyOf(Object error) => _problemsOf(error, 'job_not_ready');

  static CertificateIncompleteFailure? _problemsOf(Object error, String code) {
    if (error is! DioException || error.response?.statusCode != 409) return null;
    final data = error.response?.data;
    if (data is! Map || data['code'] != code) return null;
    final problems = data['problems'];
    return CertificateIncompleteFailure([
      if (problems is List)
        for (final problem in problems)
          if (_problemText(problem) case final text? when text.isNotEmpty) text,
    ]);
  }

  /// One problem as the API sends it: a sentence, or an object carrying one.
  static String? _problemText(Object? problem) {
    final text = problem is Map ? problem['message'] ?? problem['label'] : problem;
    return text?.toString().trim();
  }

  @override
  Future<void> approveMachine(String requestId, String requestItemId) async {
    // No body: the API signs with the signature it already holds.
    final result = await _safeApi<void>(
      () => _api.approveMachine(requestId, requestItemId, const {}),
    );
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  @override
  Future<void> returnMachine(String requestId, String requestItemId, {required String note}) async {
    final result = await _safeApi<void>(
      () => _api.returnMachine(requestId, requestItemId, {'note': note}),
    );
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  @override
  Future<void> approveAll(
    String requestId, {
    List<List<double>>? signature,
    bool rememberSignature = false,
  }) async {
    // The body is optional: without a signature the API signs with the one
    // it already holds.
    final body = <String, dynamic>{
      if (signature != null && signature.isNotEmpty) ...{
        'signature': jsonEncode(signature),
        'rememberSignature': rememberSignature,
      },
    };
    final result = await _safeApi<void>(
      () => _api.approveAll(requestId, body),
      mapError: _notReadyOf,
    );
    result.fold((_) {}, (f) => throw f is CertificateIncompleteFailure ? f : _mapError(f));
  }

  @override
  Future<void> returnToInspector(
    String requestId, {
    required String note,
    Map<String, String> machineComments = const {},
  }) async {
    final body = <String, dynamic>{
      'note': note,
      if (machineComments.isNotEmpty)
        'machines': [
          for (final entry in machineComments.entries) {'requestItemId': entry.key, 'comment': entry.value},
        ],
    };
    final result = await _safeApi<void>(() => _api.returnToInspector(requestId, body));
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  @override
  Future<DeliveryPreview> getDeliveryPreview(String requestId) async {
    final result = await _safeApi(
      () async => MachinesParser.deliveryPreview(await _api.getDeliveryPreview(requestId) as Map<String, dynamic>),
    );
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<DeliveryResult> deliverToClient(String requestId, {String? sentTo, String? message}) async {
    final body = <String, dynamic>{
      if (sentTo != null && sentTo.isNotEmpty) 'sentTo': sentTo,
      if (message != null && message.isNotEmpty) 'message': message,
    };
    final result = await _safeApi(
      () async => MachinesParser.deliveryResult(await _api.deliverToClient(requestId, body)),
    );
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<Uint8List> getMachinePdf(String requestId, String requestItemId) async {
    final result = await _safeApi(
      () async => Uint8List.fromList(await _api.getMachinePdf(requestId, requestItemId)),
    );
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<void> startInspection(String requestId) async {
    final result = await _safeApi<void>(() => _api.startInspection(requestId));
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  RequestsFailure _mapError(Object error) {
    if (error is RequestsFailure) return error;
    return RequestsFailure(switch (ApiErrorHandler.classify(error)) {
      ApiErrorKind.permissionDenied => RequestsFailureCode.permissionDenied,
      ApiErrorKind.conflict => RequestsFailureCode.conflict,
      ApiErrorKind.network => RequestsFailureCode.network,
      ApiErrorKind.unknown => RequestsFailureCode.unknown,
    });
  }
}
