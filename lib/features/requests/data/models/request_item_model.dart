import '../../domain/entities/request_item.dart';

class RequestItemModel {
  final String id;
  final String type;
  final String? category;
  final String? manufacturer;
  final String? model;
  final String? serialNumber;
  final String? prNumber;
  final String? capacity;
  final int quantity;

  const RequestItemModel({
    required this.id,
    required this.type,
    this.category,
    this.manufacturer,
    this.model,
    this.serialNumber,
    this.prNumber,
    this.capacity,
    this.quantity = 1,
  });

  factory RequestItemModel.fromJson(Map<String, dynamic> json) {
    return RequestItemModel(
      id: json['id'] as String? ?? '',
      type: json['type'] as String? ?? '',
      category: json['category'] as String?,
      manufacturer: json['manufacturer'] as String?,
      model: json['model'] as String?,
      serialNumber: json['serialNumber'] as String?,
      prNumber: json['prNumber'] as String?,
      capacity: json['capacity'] as String?,
      quantity: (json['quantity'] as num?)?.toInt() ?? 1,
    );
  }

  Map<String, dynamic> toJson() => {
    'id': id,
    'type': type,
    'category': category,
    'manufacturer': manufacturer,
    'model': model,
    'serialNumber': serialNumber,
    'prNumber': prNumber,
    'capacity': capacity,
    'quantity': quantity,
  };

  RequestItem toEntity() => RequestItem(
    id: id,
    type: type,
    category: category,
    manufacturer: manufacturer,
    model: model,
    serialNumber: serialNumber,
    prNumber: prNumber,
    capacity: capacity,
    quantity: quantity,
  );
}
