/// What a priced line is charged per — the "Unit of Pricing" column of the
/// proposal's budget table.
enum PricingUnit {
  perVisit('per_visit', 'Per Visit'),
  perDay('per_day', 'Per Day'),
  perHour('per_hour', 'Per Hour'),
  perItem('per_item', 'Per Item');

  final String value;

  /// How the proposal PDF prints it — a document for the client, so it
  /// stays English whatever language the app is in.
  final String pdfLabel;

  const PricingUnit(this.value, this.pdfLabel);

  static PricingUnit fromValue(Object? value) {
    for (final unit in values) {
      if (unit.value == value) return unit;
    }
    return PricingUnit.perVisit;
  }
}
