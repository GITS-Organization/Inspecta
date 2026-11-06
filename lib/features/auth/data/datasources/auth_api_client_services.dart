import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../../../../core/networking/api_urls.dart';
import '../models/auth_requests.dart';
import '../models/auth_session_model.dart';

part 'auth_api_client_services.g.dart';

@RestApi()
abstract class AuthApiClientServices {
  factory AuthApiClientServices(Dio dio, {String? baseUrl}) =
      _AuthApiClientServices;

  @POST(ApiUrls.login)
  Future<AuthSessionModel> login(@Body() LoginRequest request);

  @POST(ApiUrls.refresh)
  Future<AuthSessionModel> refresh(@Body() RefreshTokenRequest request);

  @POST(ApiUrls.logout)
  Future<void> logout(
    @Header('Authorization') String authorization,
    @Body() RefreshTokenRequest request,
  );
}
