import 'quotation.dart';
import 'quotation_status.dart';
import 'reply_type.dart';

/// The Quotations tracker's three tabs. A draft is work in progress and
/// still unanswered, so it is listed under Open (marked Draft). A quote the
/// client countered is also unresolved, so it stays under Open too.
enum QuoteBoardTab { open, accepted, rejected }

extension QuoteBoardTabX on QuoteBoardTab {
  static QuoteBoardTab? of(Quotation q, {DateTime? now}) {
    // A rejection is always terminal, regardless of its stored status.
    if (q.type == ReplyType.reject) return QuoteBoardTab.rejected;

    return switch (q.status) {
      QuotationStatus.superseded => null,
      QuotationStatus.draft => QuoteBoardTab.open,
      QuotationStatus.accepted => QuoteBoardTab.accepted,
      QuotationStatus.declined => QuoteBoardTab.rejected,
      QuotationStatus.expired => QuoteBoardTab.rejected,
      QuotationStatus.countered => QuoteBoardTab.open,
      QuotationStatus.sent => q.isExpired(now: now) ? QuoteBoardTab.rejected : QuoteBoardTab.open,
    };
  }
}
