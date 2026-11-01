import '../entities/inspection_request.dart';
import '../entities/request_item.dart';
import '../repositories/requests_repository.dart';

/// Fills in a request's client, location, access notes and equipment by hand
/// so it can be quoted (BR-02.5) — how an emailed request, which arrives
/// with none of them, is completed.
class CompleteIntake {
  final RequestsRepository _repository;

  const CompleteIntake(this._repository);

  Future<void> call({
    required InspectionRequest request,
    required String clientName,
    required String location,
    String? clientId,
    String? accessNotes,
    required List<RequestItem> items,
  }) => _repository.completeIntake(
    request: request,
    clientName: clientName.trim(),
    location: location.trim(),
    clientId: clientId,
    accessNotes: accessNotes?.trim().isEmpty ?? true ? null : accessNotes!.trim(),
    items: items,
  );
}
