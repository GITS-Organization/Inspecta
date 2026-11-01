import 'package:dio/dio.dart';

import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/error/api_error_handler.dart';
import '../../domain/entities/client.dart';
import '../../domain/entities/clients_failure.dart';
import '../../domain/repositories/clients_repository.dart';
import '../datasources/clients_api_client_services.dart';

class ClientsRepositoryImpl implements ClientsRepository {
  static const _safeApi = SafeApi();

  final ClientsApiClientServices _api;

  const ClientsRepositoryImpl(this._api);

  static List<Map<String, dynamic>> _maps(Object? rows) =>
      rows is List ? rows.whereType<Map<String, dynamic>>().toList() : const [];

  static String? _text(Object? value) => value is String && value.trim().isNotEmpty ? value.trim() : null;

  static ClientEmail _email(Map<String, dynamic> json) => ClientEmail(
    id: json['id'] as String? ?? '',
    pattern: json['pattern'] as String? ?? '',
    isDomain: json['isDomain'] == true,
    promotedRequests: (json['promotedRequests'] as num?)?.toInt() ?? 0,
  );

  @override
  Future<List<Client>> getClients() async {
    final result = await _safeApi(() async {
      return [
        for (final json in _maps(await _api.getClients()))
          if (json['id'] is String)
            Client(
              id: json['id'] as String,
              name: json['name'] as String? ?? '',
              location: _text(json['location']),
              certificatesEmail: _text(json['certificatesEmail']),
            ),
      ];
    });
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<List<ClientMachine>> getMachines(String clientId) async {
    final result = await _safeApi(() async {
      return [
        for (final json in _maps(await _api.getClientMachines(clientId)))
          if (json['id'] is String)
            ClientMachine(
              id: json['id'] as String,
              name: json['name'] as String? ?? '',
              category: _text(json['category']),
              manufacturer: _text(json['manufacturer']),
              model: _text(json['model']),
              serialNumber: _text(json['serialNumber']),
              capacity: _text(json['capacity']),
            ),
      ];
    });
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<List<ClientEmail>> getEmails(String clientId) async {
    final result = await _safeApi(() async => _maps(await _api.getClientEmails(clientId)).map(_email).toList());
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<ClientEmail> addEmail(String clientId, String pattern) async {
    final result = await _safeApi(
      () async => _email(await _api.addClientEmail(clientId, {'pattern': pattern}) as Map<String, dynamic>),
      mapError: _statusFailureOf,
    );
    return result.fold((data) => data, (f) => throw _mapError(f));
  }

  @override
  Future<void> removeEmail(String clientId, String ruleId) async {
    final result = await _safeApi<void>(
      () => _api.removeClientEmail(clientId, ruleId),
      mapError: _statusFailureOf,
    );
    result.fold((_) {}, (f) => throw _mapError(f));
  }

  /// The answers adding or removing a sender has of its own: 400 not a valid
  /// address or domain, 404 no such client (or rule), 409 already registered.
  static ClientsFailure? _statusFailureOf(Object error) {
    if (error is! DioException) return null;
    return switch (error.response?.statusCode) {
      400 => const ClientsFailure(ClientsFailureCode.invalidPattern),
      404 => const ClientsFailure(ClientsFailureCode.notFound),
      409 => const ClientsFailure(ClientsFailureCode.duplicate),
      _ => null,
    };
  }

  ClientsFailure _mapError(Object error) {
    if (error is ClientsFailure) return error;
    return ClientsFailure(switch (ApiErrorHandler.classify(error)) {
      ApiErrorKind.permissionDenied => ClientsFailureCode.permissionDenied,
      ApiErrorKind.network => ClientsFailureCode.network,
      ApiErrorKind.conflict || ApiErrorKind.unknown => ClientsFailureCode.unknown,
    });
  }
}
