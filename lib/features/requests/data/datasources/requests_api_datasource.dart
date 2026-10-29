import 'package:dio/dio.dart';

import '../../../../core/base/result.dart';
import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/networking/api_urls.dart';
import '../../../../core/networking/dio_factory.dart';
import '../../domain/entities/request_item.dart';
import '../../domain/entities/request_triage.dart';
import '../models/inspection_request_model.dart';

/// Requests read from the Inspecta API. The rest of the requests feature
/// still goes through Firestore ([RequestsRemoteDataSource]).
class RequestsApiDataSource {
  static const _safeApi = SafeApi();

  final Dio? _dioOverride;

  RequestsApiDataSource({Dio? dio}) : _dioOverride = dio;

  Dio get _dio => _dioOverride ?? DioFactory.getDio();

  /// One page of the signed-in user's requests, newest first. The API caps
  /// [pageSize] at 100.
  Future<Result<List<InspectionRequestModel>>> getRequests({
    required RequestTriage triage,
    String? query,
    int page = 1,
    int pageSize = 20,
  }) {
    return _safeApi(() async {
      final response = await _dio.get<List<dynamic>>(
        ApiUrls.requests,
        queryParameters: {
          'triage': triage.value,
          'q': ?query,
          'page': page,
          'pageSize': pageSize,
        },
      );
      return [
        for (final json in response.data!.whereType<Map<String, dynamic>>())
          InspectionRequestModel.fromApiJson(json),
      ];
    });
  }

  /// Turns a "maybe" email into a real request and returns it as it now is.
  Future<Result<InspectionRequestModel>> confirmRequest(String id) {
    return _safeApi(() async {
      final response = await _dio.post<Map<String, dynamic>>(
        '${ApiUrls.requests}/$id/confirm',
      );
      return InspectionRequestModel.fromApiJson(response.data!);
    });
  }

  Future<Result<InspectionRequestModel>> updateRequest(
    String id, {
    String? clientId,
    required String clientName,
    required String location,
    DateTime? preferredDate,
    String? accessNotes,
    required List<RequestItem> items,
    DateTime? expectedUpdatedAt,
  }) {
    return _safeApi(() async {
      final response = await _dio.put<Map<String, dynamic>>(
        '${ApiUrls.requests}/$id',
        data: {
          'clientId': ?clientId,
          'clientName': clientName,
          'location': location,
          'preferredDate': ?preferredDate?.toUtc().toIso8601String(),
          'accessNotes': ?accessNotes,
          'items': [
            for (final item in items)
              {
                if (item.id.isNotEmpty) 'id': item.id,
                'name': item.type,
                'type': item.type,
                'quantity': item.quantity,
                'capacity': ?item.capacity,
                'serialNumber': ?item.serialNumber,
                'location': item.location ?? location,
              },
          ],
          'expectedUpdatedAt': ?expectedUpdatedAt?.toUtc().toIso8601String(),
        },
      );
      return InspectionRequestModel.fromApiJson(response.data!);
    });
  }

  Future<Result<void>> deleteRequest(String id) {
    return _safeApi<void>(() async {
      await _dio.delete<void>('${ApiUrls.requests}/$id');
    });
  }
}
