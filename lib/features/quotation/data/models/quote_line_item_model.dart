import '../../domain/entities/pricing_unit.dart';
import '../../domain/entities/quote_line_item.dart';

class QuoteLineItemModel {
  final String requestItemId;
  final String type;
  final int quantity;
  final int unitPricePiastres;
  final PricingUnit pricingUnit;

  const QuoteLineItemModel({
    required this.requestItemId,
    required this.type,
    required this.quantity,
    required this.unitPricePiastres,
    this.pricingUnit = PricingUnit.perVisit,
  });

  factory QuoteLineItemModel.fromJson(Map<String, dynamic> json) {
    return QuoteLineItemModel(
      requestItemId: json['requestItemId'] as String? ?? '',
      type: json['type'] as String? ?? '',
      quantity: (json['quantity'] as num?)?.toInt() ?? 1,
      unitPricePiastres: (json['unitPricePiastres'] as num?)?.toInt() ?? 0,
      pricingUnit: PricingUnit.fromValue(json['pricingUnit']),
    );
  }

  factory QuoteLineItemModel.fromEntity(QuoteLineItem item) => QuoteLineItemModel(
    requestItemId: item.requestItemId,
    type: item.type,
    quantity: item.quantity,
    unitPricePiastres: item.unitPricePiastres,
    pricingUnit: item.pricingUnit,
  );

  Map<String, dynamic> toJson() => {
    'requestItemId': requestItemId,
    'type': type,
    'quantity': quantity,
    'unitPricePiastres': unitPricePiastres,
    'pricingUnit': pricingUnit.value,
  };

  QuoteLineItem toEntity() => QuoteLineItem(
    requestItemId: requestItemId,
    type: type,
    quantity: quantity,
    unitPricePiastres: unitPricePiastres,
    pricingUnit: pricingUnit,
  );
}
