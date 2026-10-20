import 'package:equatable/equatable.dart';

import 'pricing_unit.dart';

/// One request item priced on a quotation (BR-03.2: price is per unit,
/// total = unit price × quantity).
class QuoteLineItem extends Equatable {
  final String requestItemId;
  final String type;
  final int quantity;
  final int unitPricePiastres;
  final PricingUnit pricingUnit;

  const QuoteLineItem({
    required this.requestItemId,
    required this.type,
    required this.quantity,
    required this.unitPricePiastres,
    this.pricingUnit = PricingUnit.perVisit,
  });

  int get lineTotalPiastres => unitPricePiastres * quantity;

  @override
  List<Object?> get props => [requestItemId, type, quantity, unitPricePiastres, pricingUnit];
}
