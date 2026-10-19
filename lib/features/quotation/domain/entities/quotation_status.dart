/// A quotation's lifecycle (Feature 03 §6, extended with `declined` so a
/// client's decline is distinguishable from the coordinator's own
/// [ReplyType.reject]).
enum QuotationStatus {
  draft('draft'),
  sent('sent'),
  countered('countered'),
  accepted('accepted'),
  declined('declined'),
  expired('expired'),
  superseded('superseded');

  final String value;

  const QuotationStatus(this.value);

  static QuotationStatus? fromValue(Object? value) {
    for (final status in values) {
      if (status.value == value) return status;
    }
    return null;
  }
}
