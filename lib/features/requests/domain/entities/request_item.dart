import 'package:equatable/equatable.dart';

/// One piece of equipment on an [InspectionRequest]. Each item is quoted
/// and certified separately (BR-02.6).
class RequestItem extends Equatable {
  final String id;
  final String type;
  final String? category;
  final String? manufacturer;
  final String? model;
  final String? serialNumber;

  /// The client's purchase-request number for this item — printed in the
  /// proposal's scope-of-work table.
  final String? prNumber;

  /// Free-text capacity / SWL, e.g. "10 t" (00-overview.md's `Equipment`).
  final String? capacity;
  final int quantity;

  /// Where this particular item sits, when it differs from the request's
  /// site-wide [InspectionRequest.location].
  final String? location;

  const RequestItem({
    required this.id,
    required this.type,
    this.category,
    this.manufacturer,
    this.model,
    this.serialNumber,
    this.prNumber,
    this.capacity,
    this.quantity = 1,
    this.location,
  });

  /// "Overhead crane — 10 t" for a card title; just the type if there's no
  /// capacity on file yet.
  String get displayTitle => capacity == null || capacity!.isEmpty
      ? type
      : '$type — $capacity';

  @override
  List<Object?> get props => [
    id,
    type,
    category,
    manufacturer,
    model,
    serialNumber,
    prNumber,
    capacity,
    quantity,
    location,
  ];
}
