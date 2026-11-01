import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../../../../core/networking/api_urls.dart';

part 'clients_api_client_services.g.dart';

@RestApi()
abstract class ClientsApiClientServices {
  factory ClientsApiClientServices(Dio dio, {String? baseUrl}) = _ClientsApiClientServices;

  /// The clients of the signed-in user's team, by name.
  @GET(ApiUrls.clients)
  Future<dynamic> getClients();

  /// The machines of one client.
  @GET(ApiUrls.equipment)
  Future<dynamic> getClientMachines(@Query('clientId') String clientId);

  /// The email addresses and domains registered for the client (the senders
  /// whose emails become its requests).
  @GET('${ApiUrls.clients}/{clientId}/emails')
  Future<dynamic> getClientEmails(@Path('clientId') String clientId);

  /// Registers another address or domain for the client. Answers with the
  /// rule and `promotedRequests`, how many "maybe" emails from this sender
  /// became requests of the client.
  @POST('${ApiUrls.clients}/{clientId}/emails')
  Future<dynamic> addClientEmail(
    @Path('clientId') String clientId,
    @Body() Map<String, dynamic> body,
  );

  /// Removes an address or domain from the client.
  @DELETE('${ApiUrls.clients}/{clientId}/emails/{ruleId}')
  Future<void> removeClientEmail(
    @Path('clientId') String clientId,
    @Path('ruleId') String ruleId,
  );
}
