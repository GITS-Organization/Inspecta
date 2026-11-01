import 'package:intl/intl.dart';

/// Money is stored as integer minor units (piastres for EGP, cents for USD
/// and EUR…) to avoid rounding errors (00-overview.md §8, BR-03.10) and
/// displayed as the ISO code and the amount, e.g. "EGP 12,500.00".
///
/// Every currency listed in [supported] has two decimals, so one factor of
/// 100 turns minor units into the amount for all of them.
class Currency {
  Currency._();

  /// The default, and what an amount without a currency is taken to be in.
  static const egp = 'EGP';

  /// The ISO 4217 codes a quote can be priced in.
  static const supported = [egp, 'USD', 'EUR', 'GBP', 'SAR', 'AED'];

  static final _format = NumberFormat('#,##0.00', 'en');

  /// [code] when it's one of [supported] (any case), otherwise [egp].
  static String normalize(String? code) {
    final upper = code?.trim().toUpperCase();
    return upper != null && supported.contains(upper) ? upper : egp;
  }

  /// "EGP 12,500.00" / "USD 5,000.00" from [minorUnits] in [code].
  static String format(int minorUnits, {String code = egp}) =>
      '${normalize(code)} ${_format.format(minorUnits / 100)}';

  /// Parses a price typed in whole units (e.g. "4500" or "4,500.50") into
  /// integer minor units. Returns `null` for empty or unparsable input.
  static int? parsePiastres(String input) {
    final cleaned = input.replaceAll(',', '').trim();
    if (cleaned.isEmpty) return null;
    final amount = double.tryParse(cleaned);
    if (amount == null) return null;
    return (amount * 100).round();
  }

  /// The text a price field should start with for a saved amount — "4500"
  /// or "4500.50", no separators or trailing ".00" — so it reads back
  /// through [parsePiastres] unchanged.
  static String toInputText(int piastres) =>
      piastres % 100 == 0 ? '${piastres ~/ 100}' : (piastres / 100).toStringAsFixed(2);
}
