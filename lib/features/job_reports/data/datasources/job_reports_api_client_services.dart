import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../../../../core/networking/api_urls.dart';

part 'job_reports_api_client_services.g.dart';

@RestApi()
abstract class JobReportsApiClientServices {
  factory JobReportsApiClientServices(Dio dio, {String? baseUrl}) = _JobReportsApiClientServices;

  /// The job's live reports with the limits and what the user may do.
  @GET('${ApiUrls.quotationQueue}/{requestId}/reports')
  Future<dynamic> getReports(@Path('requestId') String requestId);

  /// Adds one file: `{ fileName, contentBase64, requestItemId? }`. Answers 201
  /// with the file.
  @POST('${ApiUrls.quotationQueue}/{requestId}/reports')
  Future<dynamic> uploadReport(
    @Path('requestId') String requestId,
    @Body() Map<String, dynamic> body,
  );

  /// The file itself, as a download.
  @GET('${ApiUrls.quotationQueue}/{requestId}/reports/{fileId}')
  @DioResponseType(ResponseType.bytes)
  Future<List<int>> downloadReport(
    @Path('requestId') String requestId,
    @Path('fileId') String fileId,
  );

  /// Removes a file; answers with the new list.
  @DELETE('${ApiUrls.quotationQueue}/{requestId}/reports/{fileId}')
  Future<dynamic> deleteReport(
    @Path('requestId') String requestId,
    @Path('fileId') String fileId,
  );

  /// Every live report of the job in one ZIP.
  @GET('${ApiUrls.quotationQueue}/{requestId}/reports.zip')
  @DioResponseType(ResponseType.bytes)
  Future<List<int>> downloadReportsZip(@Path('requestId') String requestId);
}
