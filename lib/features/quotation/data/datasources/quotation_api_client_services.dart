import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../../../../core/networking/api_urls.dart';
import '../models/quotation_bodies.dart';

part 'quotation_api_client_services.g.dart';

@RestApi()
abstract class QuotationApiClientServices {
  factory QuotationApiClientServices(Dio dio, {String? baseUrl}) =
      _QuotationApiClientServices;

  /// One page of the signed-in user's quotes across all requests, as list
  /// rows (no line items).
  @GET(ApiUrls.quotations)
  Future<dynamic> getQuotations(
    @Query('includeSuperseded') bool includeSuperseded,
    @Query('page') int page,
    @Query('pageSize') int pageSize,
  );

  /// Opens a quote on the request — the machines unpriced — and returns it as
  /// the API recorded it (with its id, version and number).
  @POST('${ApiUrls.requests}/{requestId}/quotes')
  Future<dynamic> sendQuote(
    @Path('requestId') String requestId,
    @Body() SendQuoteBody body,
  );

  /// The email that would carry the quote's PDF, ready to edit: `to`, `cc`,
  /// `subject`, `body`, and whether it can be sent. [action] is `send` (an
  /// offer or counter offer) or `accept`. Sends nothing.
  @GET('${ApiUrls.requests}/{requestId}/quotes/{quoteId}/email')
  Future<dynamic> getQuoteEmail(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
    @Query('action') String action,
  );

  /// Sends the existing quote to the client by email. The body is optional:
  /// `sentTo`, `cc`, `subject` and `message` each replace the suggestion. The
  /// response may carry its version.
  @POST('${ApiUrls.requests}/{requestId}/quotes/{quoteId}/send')
  Future<dynamic> sendExistingQuote(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
    @Body() Map<String, dynamic> body,
  );

  /// Accepts the quote and emails its PDF, with the same optional body as
  /// [sendExistingQuote]. The response may carry its version.
  @POST('${ApiUrls.requests}/{requestId}/quotes/{quoteId}/accept')
  Future<dynamic> acceptQuote(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
    @Body() Map<String, dynamic> body,
  );

  /// Saves the priced offer onto the quote.
  @PUT('${ApiUrls.requests}/{requestId}/quotes/{quoteId}')
  Future<void> updateQuote(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
    @Body() UpdateQuoteBody body,
  );

  /// Every quote of the request, with the client's replies to the current one
  /// (`clientReplies`).
  @GET('${ApiUrls.requests}/{requestId}/quotes')
  Future<dynamic> getRequestQuotes(@Path('requestId') String requestId);

  /// Opens a new revision of a sent quote with the same prices, to edit and
  /// send again; the request goes back to `quote_draft`. 201 with the new
  /// quote; 409 unless the request is `quote_sent`, `counter_offer_sent` or
  /// `client_countered`.
  @POST('${ApiUrls.requests}/{requestId}/quotes/{quoteId}/reopen')
  Future<dynamic> reopenQuote(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
  );

  /// Clears the client's replies and marks them read; the request goes back to
  /// waiting for the client. 200 with the quote; 409 when there is no reply.
  @POST('${ApiUrls.requests}/{requestId}/quotes/{quoteId}/dismiss-reply')
  Future<dynamic> dismissReply(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
  );

  /// The proposal PDF the API generates for the quote.
  @GET('${ApiUrls.requests}/{requestId}/quotes/{quoteId}/pdf')
  @DioResponseType(ResponseType.bytes)
  Future<List<int>> getQuotePdf(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
  );
}
