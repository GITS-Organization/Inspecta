/// What a priced line is charged per — the "Unit of Pricing" column of the
/// proposal's budget table. Every offer is a fixed price per visit; older
/// quotations may have been saved with another unit, and read back as this.
enum PricingUnit {
  perVisit('per_visit', 'Per Visit');

  final String value;

  /// How the proposal PDF prints it — a document for the client, so it
  /// stays English whatever language the app is in.
  final String pdfLabel;

  const PricingUnit(this.value, this.pdfLabel);

  static PricingUnit fromValue(Object? value) => PricingUnit.perVisit;
}
