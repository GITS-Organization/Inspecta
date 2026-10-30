import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../../../../core/networking/api_urls.dart';

part 'coordinator_api_client_services.g.dart';

@RestApi()
abstract class CoordinatorApiClientServices {
  factory CoordinatorApiClientServices(Dio dio, {String? baseUrl}) =
      _CoordinatorApiClientServices;

  /// The active inspectors of the signed-in user's team, with whether each is
  /// on leave and how many unfinished jobs they have. [managerId] (Admin
  /// only) picks another team.
  @GET(ApiUrls.inspectors)
  Future<dynamic> getInspectors(@Query('managerId') String? managerId);
}
