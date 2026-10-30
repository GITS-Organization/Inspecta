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

  /// Sends the quote of the request, returning it as the API recorded it
  /// (with its version and number).
  @POST('${ApiUrls.requests}/{requestId}/quotes')
  Future<dynamic> sendQuote(
    @Path('requestId') String requestId,
    @Body() SendQuoteBody body,
  );

  /// Sends the existing quote to the client (no body). The response may
  /// carry its version.
  @POST('${ApiUrls.requests}/{requestId}/quotes/{quoteId}/send')
  Future<dynamic> sendExistingQuote(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
  );

  /// Accepts the quote (no body). The response may carry its version.
  @POST('${ApiUrls.requests}/{requestId}/quotes/{quoteId}/accept')
  Future<dynamic> acceptQuote(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
  );

  /// Saves the priced offer onto the quote.
  @PUT('${ApiUrls.requests}/{requestId}/quotes/{quoteId}')
  Future<void> updateQuote(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
    @Body() UpdateQuoteBody body,
  );

  /// The proposal PDF the API generates for the quote.
  @GET('${ApiUrls.requests}/{requestId}/quotes/{quoteId}/pdf')
  @DioResponseType(ResponseType.bytes)
  Future<List<int>> getQuotePdf(
    @Path('requestId') String requestId,
    @Path('quoteId') String quoteId,
  );
}
