import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../../../../core/networking/api_urls.dart';

part 'queue_api_client_services.g.dart';

@RestApi()
abstract class QueueApiClientServices {
  factory QueueApiClientServices(Dio dio, {String? baseUrl}) =
      _QueueApiClientServices;

  /// [status] picks one of the queue's lists (`unassigned`, `certify`…) and
  /// [q] searches it; both are left out when null.
  @GET(ApiUrls.quotationQueue)
  Future<dynamic> getQueue(
    @Query('managerId') String? managerId,
    @Query('status') String? status,
    @Query('q') String? q,
    @Query('page') int page,
    @Query('pageSize') int pageSize,
  );

  /// Jobs with machine certificates waiting on the technical manager's
  /// review, each row carrying its per-stage certificate counts.
  @GET(ApiUrls.quotationQueueToReview)
  Future<dynamic> getToReview(
    @Query('managerId') String? managerId,
    @Query('inspectorId') String? inspectorId,
    @Query('page') int page,
    @Query('pageSize') int pageSize,
  );
}
