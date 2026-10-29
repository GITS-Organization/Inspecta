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

  /// Every quote of one request, one per version.
  Future<Result<List<QuotationModel>>> getRequestQuotes(String requestId) {
    return _safeApi(() async {
      final response = await _dio.get<List<dynamic>>(
        '${ApiUrls.requests}/$requestId/quotes',
      );
      return [
        for (final json in response.data!.whereType<Map<String, dynamic>>())
          QuotationModel.fromApiJson(json),
      ];
    });
  }
}
