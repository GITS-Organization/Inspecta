import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../../../../core/networking/api_urls.dart';

part 'machines_api_client_services.g.dart';

@RestApi()
abstract class MachinesApiClientServices {
  factory MachinesApiClientServices(Dio dio, {String? baseUrl}) = _MachinesApiClientServices;

  /// The machines to inspect on a job, one row each, with the job's progress
  /// and the buttons the current user may press.
  @GET('${ApiUrls.quotationQueue}/{requestId}/machines')
  Future<dynamic> getMachines(@Path('requestId') String requestId);

  /// One machine's whole form: header, technical fields, checklist and what
  /// was saved so far.
  @GET('${ApiUrls.quotationQueue}/{requestId}/machines/{requestItemId}')
  Future<dynamic> getMachineForm(
    @Path('requestId') String requestId,
    @Path('requestItemId') String requestItemId,
  );

  /// Saves a partial update of the machine's form; answers with the form as
  /// it now stands.
  @PUT('${ApiUrls.quotationQueue}/{requestId}/machines/{requestItemId}')
  Future<dynamic> saveMachineForm(
    @Path('requestId') String requestId,
    @Path('requestItemId') String requestItemId,
    @Body() Map<String, dynamic> body,
  );

  /// Submits the machine's certificate to the technical manager (no body).
  @POST('${ApiUrls.quotationQueue}/{requestId}/machines/{requestItemId}/submit')
  Future<dynamic> submitMachine(
    @Path('requestId') String requestId,
    @Path('requestItemId') String requestItemId,
  );

  /// The technical manager approves the machine's certificate, signing it.
  @POST('${ApiUrls.quotationQueue}/{requestId}/machines/{requestItemId}/approve')
  Future<dynamic> approveMachine(
    @Path('requestId') String requestId,
    @Path('requestItemId') String requestItemId,
    @Body() Map<String, dynamic> body,
  );

  /// The technical manager sends the machine's certificate back to its
  /// inspector with a note.
  @POST('${ApiUrls.quotationQueue}/{requestId}/machines/{requestItemId}/return')
  Future<dynamic> returnMachine(
    @Path('requestId') String requestId,
    @Path('requestItemId') String requestItemId,
    @Body() Map<String, dynamic> body,
  );

  /// The machine's certificate as a PDF: marked DRAFT until it is approved,
  /// then the stored final file.
  @GET('${ApiUrls.quotationQueue}/{requestId}/machines/{requestItemId}/pdf')
  @DioResponseType(ResponseType.bytes)
  Future<List<int>> getMachinePdf(
    @Path('requestId') String requestId,
    @Path('requestItemId') String requestItemId,
  );

  /// The technical manager approves every certificate of the job at once,
  /// all or nothing. The body is optional: the signature as an array of
  /// strokes (never an image) and whether to remember it.
  @POST('${ApiUrls.quotationQueue}/{requestId}/approve-all')
  Future<dynamic> approveAll(
    @Path('requestId') String requestId,
    @Body() Map<String, dynamic> body,
  );

  /// The technical manager sends the whole job back to its inspector with a
  /// required note and, optionally, a comment per machine.
  @POST('${ApiUrls.quotationQueue}/{requestId}/return-to-inspector')
  Future<dynamic> returnToInspector(
    @Path('requestId') String requestId,
    @Body() Map<String, dynamic> body,
  );

  /// The email that would go to the client: `to`, subject, text and
  /// attachments.
  @GET('${ApiUrls.quotationQueue}/{requestId}/deliver-to-client/preview')
  Future<dynamic> getDeliveryPreview(@Path('requestId') String requestId);

  /// The coordinator hands the job's certificates to the client; the job
  /// becomes `sent_to_client`. The body is optional: `sentTo` and `message`.
  @POST('${ApiUrls.quotationQueue}/{requestId}/deliver-to-client')
  Future<dynamic> deliverToClient(
    @Path('requestId') String requestId,
    @Body() Map<String, dynamic> body,
  );

  /// Begins the inspection (no body); safe to call again.
  @POST('${ApiUrls.quotationQueue}/{requestId}/start-inspection')
  Future<dynamic> startInspection(@Path('requestId') String requestId);
}
