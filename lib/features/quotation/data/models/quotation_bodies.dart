import '../../domain/entities/quote_line_item.dart';

/// The body of `POST /requests/{id}/quotes`. Each part is left out when null.
class SendQuoteBody {
  /// How long the quote stays valid.
  final int? validForDays;

  /// The new total, when sending a new price version.
  final int? amountPiastres;

  const SendQuoteBody({this.validForDays, this.amountPiastres});

  Map<String, dynamic> toJson() => {
    'validForDays': ?validForDays,
    'amountPiastres': ?amountPiastres,
  };
}

/// The body of `PUT /requests/{id}/quotes/{quoteId}`: each item's unit price,
/// how long the quote stays valid, and its scope, notes and message. Parts
/// that are null or empty are left out.
class UpdateQuoteBody {
  final List<QuoteLineItem> items;
  final int? validForDays;
  final List<String>? scopeOfWork;
  final List<String>? notes;
  final String? message;

  const UpdateQuoteBody({
    required this.items,
    this.validForDays,
    this.scopeOfWork,
    this.notes,
    this.message,
  });

  Map<String, dynamic> toJson() => {
    'items': [
      for (final item in items)
        {
          'requestItemId': item.requestItemId,
          'unitPricePiastres': item.unitPricePiastres,
        },
    ],
    'validForDays': ?validForDays,
    if (scopeOfWork != null && scopeOfWork!.isNotEmpty) 'scopeOfWork': scopeOfWork,
    if (notes != null && notes!.isNotEmpty) 'notes': notes,
    if (message != null && message!.isNotEmpty) 'message': message,
  };
}
