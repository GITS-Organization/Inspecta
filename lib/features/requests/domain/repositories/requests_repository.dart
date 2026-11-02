import '../../../../core/base/paginated.dart';
import '../entities/inspection_request.dart';
import '../entities/machine_suggestion.dart';
import '../entities/request_item.dart';
import '../entities/request_triage.dart';

/// Methods emit / throw [RequestsFailure] on expected errors.
abstract interface class RequestsRepository {
  /// One page of requests from the API for the signed-in user, newest first.
  /// [triage] picks real requests or emails from unknown senders; [query]
  /// searches client, location, subject and sender server-side.
  Future<Paginated<InspectionRequest>> listRequests({
    required RequestTriage triage,
    String? query,
    int page = 1,
    int pageSize = 20,
  });

  /// Turns a [RequestTriage.maybe] email into a real request (it joins the
  /// New list) and returns it. Its sender must already belong to a client,
  /// otherwise this throws a [ClientRequiredFailure]. Throws
  /// [RequestsFailure].
  Future<InspectionRequest> confirmRequest(String requestId);

  /// The client's machines that [requestId]'s email seems to ask about, best
  /// first; those already on the request are left out, and a request without
  /// a client has none. Throws [RequestsFailure].
  Future<List<MachineSuggestion>> getSuggestions(String requestId);

  /// Reads the email again and puts the machines it clearly names on the
  /// request (when it has a client and no items yet). Returns the request as
  /// it now is. Throws [RequestsFailure].
  Future<InspectionRequest> autofill(String requestId);

  /// Discards a [RequestTriage.maybe] email. Throws [RequestsFailure].
  Future<void> deleteRequest(String requestId);

  /// A single request, or `null` if it doesn't exist (or its status isn't
  /// one the app understands). Used when a screen only has a request's id
  /// (e.g. opening it from the quotations list).
  Future<InspectionRequest?> getById(String id);

  /// BR-03.8: rejects the request with a reason, closing it.
  Future<void> rejectRequest({required String requestId, required String reason});

  /// Feature 04: a coordinator assigns an inspector and a time slot,
  /// moving the job to `assigned`.
  Future<void> assignInspector({
    required String requestId,
    required String inspectorId,
    required DateTime scheduledAt,
    String? note,
  });

  /// Fills in a [request]'s client name, location, access notes and
  /// equipment through the API, by hand — the way to make an emailed request
  /// satisfy [InspectionRequest.isReadyToQuote]. Details of [request] that
  /// aren't passed (its client id, preferred date) are sent back unchanged,
  /// since the API replaces them as a set. [clientId] sets the client the
  /// request belongs to when it has none yet (picked from the client list);
  /// without it the request's own is kept. Throws [RequestsFailure], and an
  /// [InvalidMachineFailure] when an item's machine isn't the client's.
  /// With [prepareQuotation] the API also opens the request's quotation, ready
  /// for the quotation screen.
  Future<void> completeIntake({
    required InspectionRequest request,
    required String clientName,
    required String location,
    String? clientId,
    String? accessNotes,
    required List<RequestItem> items,
    bool prepareQuotation = false,
  });
}
