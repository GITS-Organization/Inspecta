import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../../../../core/networking/api_urls.dart';
import '../models/requests_bodies.dart';

part 'requests_api_client_services.g.dart';

@RestApi()
abstract class RequestsApiClientServices {
  factory RequestsApiClientServices(Dio dio, {String? baseUrl}) =
      _RequestsApiClientServices;

  /// One page of the signed-in user's requests, newest first. The API caps
  /// [pageSize] at 100.
  @GET(ApiUrls.requests)
  Future<dynamic> getRequests(
    @Query('triage') String triage,
    @Query('q') String? query,
    @Query('page') int page,
    @Query('pageSize') int pageSize,
  );

  /// Gives the accepted request to an inspector for [body]'s time (or hands
  /// it to someone else while it hasn't started).
  @POST('${ApiUrls.requests}/{requestId}/assignment')
  Future<void> assignInspector(
    @Path('requestId') String requestId,
    @Body() AssignInspectorBody body,
  );

  /// One request by id (a 404 comes back as a failure with that status).
  @GET('${ApiUrls.requests}/{id}')
  Future<dynamic> getRequest(@Path('id') String id);

  /// Turns a "maybe" email into a real request and returns it as it now is.
  @POST('${ApiUrls.requests}/{id}/confirm')
  Future<dynamic> confirmRequest(@Path('id') String id);

  /// Rejects the request, telling the client the reason.
  @POST('${ApiUrls.requests}/{id}/reject')
  Future<void> rejectRequest(
    @Path('id') String id,
    @Body() RejectRequestBody body,
  );

  @PUT('${ApiUrls.requests}/{id}')
  Future<dynamic> updateRequest(
    @Path('id') String id,
    @Body() UpdateRequestBody body,
  );

  @DELETE('${ApiUrls.requests}/{id}')
  Future<void> deleteRequest(@Path('id') String id);
}
