import 'package:dio/dio.dart';

import '../../../../core/base/result.dart';
import '../../../../core/base/safe_request_handler.dart';
import '../../../../core/networking/api_urls.dart';
import '../../../../core/networking/dio_factory.dart';
import '../models/quotation_model.dart';

/// Quotations read from the Inspecta API. Saving, sending and logging a
/// client's reply still go through Firestore ([QuotationRemoteDataSource]).
class QuotationApiDataSource {
  static const _safeApi = SafeApi();

  final Dio? _dioOverride;

  QuotationApiDataSource({Dio? dio}) : _dioOverride = dio;

  Dio get _dio => _dioOverride ?? DioFactory.getDio();

  /// Sends the quote of [requestId] with `POST /requests/{id}/quotes` and
  /// returns it as the API recorded it (with its version and number). The
  /// body holds only how long the quote stays valid; it is left out when
  /// [validForDays] is null.
  Future<Result<QuotationModel>> sendQuote(String requestId, {int? validForDays}) {
    return _safeApi(() async {
      final response = await _dio.post<Map<String, dynamic>>(
        '${ApiUrls.requests}/$requestId/quotes',
        data: {'validForDays': ?validForDays},
      );
      return QuotationModel.fromApiJson(response.data!);
    });
  }

  /// One page of the signed-in user's quotes across all requests, as list
  /// rows (no line items). Superseded versions are left out.
  Future<Result<List<QuotationModel>>> getQuotations({
    int page = 1,
    int pageSize = 50,
  }) {
    return _safeApi(() async {
      final response = await _dio.get<List<dynamic>>(
        ApiUrls.quotations,
        queryParameters: {
          'includeSuperseded': false,
          'page': page,
          'pageSize': pageSize,
        },
      );
      return [
        for (final json in response.data!.whereType<Map<String, dynamic>>())
          QuotationModel.fromApiListJson(json),
      ];
    });
  }
}
