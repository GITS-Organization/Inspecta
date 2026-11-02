import '../../domain/entities/request_item.dart';

/// The body of `POST /requests/{id}/reject`.
class RejectRequestBody {
  final String reason;

  const RejectRequestBody({required this.reason});

  Map<String, dynamic> toJson() => {'reason': reason};
}

/// The body of `PUT /requests/{id}`. Optional parts are left out when null.
class UpdateRequestBody {
  final String? clientId;
  final String clientName;
  final String location;
  final DateTime? preferredDate;
  final String? accessNotes;
  final List<RequestItem> items;

  /// When the request was last read; the API refuses the update if the
  /// request changed since.
  final DateTime? expectedUpdatedAt;

  /// Opens the request's quotation as part of the update, so the quotation
  /// screen has a draft to work on.
  final bool prepareQuotation;

  const UpdateRequestBody({
    required this.clientName,
    required this.location,
    required this.items,
    this.clientId,
    this.preferredDate,
    this.accessNotes,
    this.expectedUpdatedAt,
    this.prepareQuotation = false,
  });

  Map<String, dynamic> toJson() => {
    'clientId': ?clientId,
    'clientName': clientName,
    'location': location,
    'preferredDate': ?preferredDate?.toUtc().toIso8601String(),
    'accessNotes': ?accessNotes,
    'items': [
      for (final item in items)
        {
          // An item without an id is new: the API assigns one. An id it
          // doesn't know is refused, so none is invented.
          if (item.id.isNotEmpty) 'id': item.id,
          // The link to the client's machine, kept so a save doesn't undo it.
          // The API refuses a machine of another client (400).
          'equipmentId': ?item.equipmentId,
          'name': item.type,
          'type': item.type,
          'quantity': item.quantity,
          'capacity': ?item.capacity,
          'serialNumber': ?item.serialNumber,
          'location': item.location ?? location,
        },
    ],
    'expectedUpdatedAt': ?expectedUpdatedAt?.toUtc().toIso8601String(),
    if (prepareQuotation) 'prepareQuotation': true,
  };
}

/// The body of `POST /requests/{id}/assignment`.
class AssignInspectorBody {
  final String inspectorId;
  final DateTime scheduledAt;
  final String? note;

  const AssignInspectorBody({
    required this.inspectorId,
    required this.scheduledAt,
    this.note,
  });

  Map<String, dynamic> toJson() => {
    'inspectorId': inspectorId,
    'scheduledAt': scheduledAt.toUtc().toIso8601String(),
    'note': ?note,
  };
}
