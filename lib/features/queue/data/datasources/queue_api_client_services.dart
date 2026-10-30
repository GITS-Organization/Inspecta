import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../../../../core/networking/api_urls.dart';

part 'queue_api_client_services.g.dart';

@RestApi()
abstract class QueueApiClientServices {
  factory QueueApiClientServices(Dio dio, {String? baseUrl}) =
      _QueueApiClientServices;

  @GET(ApiUrls.quotationQueue)
  Future<dynamic> getQueue(
    @Query('managerId') String? managerId,
    @Query('page') int page,
    @Query('pageSize') int pageSize,
  );
}
