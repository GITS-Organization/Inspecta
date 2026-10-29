import '../../../../core/base/paginated.dart';
import '../entities/inspection_request.dart';
import '../entities/request_triage.dart';
import '../repositories/requests_repository.dart';

/// One page of the requests the API holds for one [RequestTriage]: real
/// requests from known senders, or "maybe" emails from unknown ones.
class GetTriagedRequests {
  /// Small enough for a quick first paint; more load as the list scrolls.
  static const pageSize = 20;

  final RequestsRepository _repository;

  const GetTriagedRequests(this._repository);

  Future<Paginated<InspectionRequest>> call(
    RequestTriage triage, {
    String? query,
    int page = 1,
  }) {
    return _repository.listRequests(
      triage: triage,
      query: query?.trim().isEmpty ?? true ? null : query!.trim(),
      page: page,
      pageSize: pageSize,
    );
  }
}
