import '../../../../core/base/paginated.dart';
import '../../../../core/enums/job_status.dart';
import '../entities/inspection_request.dart';
import '../entities/request_item.dart';
import '../entities/request_triage.dart';

/// Methods emit / throw [RequestsFailure] on expected errors.
abstract interface class RequestsRepository {
  /// The shared requests inbox (BR-02.1), newest first. Every coordinator
  /// sees every request — there's no per-coordinator split.
  Stream<List<InspectionRequest>> watchRequests();

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
  /// New list) and returns it. Throws [RequestsFailure].
  Future<InspectionRequest> confirmRequest(String requestId);

  /// Discards a [RequestTriage.maybe] email. Throws [RequestsFailure].
  Future<void> deleteRequest(String requestId);

  /// Feature 05: every job ever assigned to one inspector — the only
  /// slice of `requests` their read access covers (see firestore.rules).
  Stream<List<InspectionRequest>> watchAssignedRequests(String inspectorId);

  /// A single request, or `null` if it doesn't exist (or its status isn't
  /// one the app understands). Used when a screen only has a request's id
  /// (e.g. opening it from the quotations list).
  Future<InspectionRequest?> getById(String id);

  /// Moves a request to [status] and appends a `JobEvent` to its history
  /// (00-overview.md §7/§8: every status change is logged).
  Future<void> updateStatus(String requestId, JobStatus status, {String? note});

  /// Moves a request to [JobStatus.certificateSubmitted] like [updateStatus],
  /// and records when — see [InspectionRequest.certificateSubmittedAt].
  Future<void> markCertificateSubmitted(String requestId);

  /// BR-03.8: rejects the request with a reason, closing it.
  Future<void> rejectRequest({required String requestId, required String reason});

  /// Feature 04: a coordinator assigns an inspector and a time slot,
  /// moving the job to [JobStatus.assigned].
  Future<void> assignInspector({
    required String requestId,
    required String inspectorId,
    required DateTime scheduledAt,
    String? note,
  });

  /// Feature 05: the assigned inspector turns down a job, handing it
  /// back to the coordinator's "ready to assign" queue.
  Future<void> declineAssignment({required String requestId, String? reason});

  /// Fills in a request's equipment and location — the only way today to
  /// make an intake-only request (e.g. one created straight from an
  /// email, before either is known) satisfy [InspectionRequest.isReadyToQuote].
  Future<void> completeIntake({
    required String requestId,
    required String location,
    required List<RequestItem> items,
  });
}
