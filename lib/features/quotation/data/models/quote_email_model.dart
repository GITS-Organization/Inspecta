import '../../domain/entities/quote_email.dart';

/// Reads the quote email API's JSON (`GET …/quotes/{id}/email`) into entities
/// and writes the optional send/accept body. Every field is optional on the
/// wire, so each reads defensively.
class QuoteEmailModel {
  QuoteEmailModel._();

  static String? _text(Object? value) => value is String && value.trim().isNotEmpty ? value : null;

  static List<String> _strings(Object? value) =>
      value is List ? [for (final v in value) if (v is String && v.trim().isNotEmpty) v.trim()] : const [];

  static QuoteEmailPreview fromJson(Map<String, dynamic> json) {
    final blockers = _strings(json['blockers']);
    return QuoteEmailPreview(
      to: _text(json['to']),
      cc: _strings(json['cc']),
      subject: json['subject'] is String ? json['subject'] as String : '',
      body: json['body'] is String ? json['body'] as String : '',
      from: _text(json['from']),
      attachmentName: _text(json['attachmentName']),
      emailEnabled: json['emailEnabled'] is bool ? json['emailEnabled'] as bool : true,
      notice: _text(json['notice']),
      // Without a flag, the email can go unless the API names a blocker.
      canSend: json['canSend'] is bool ? json['canSend'] as bool : blockers.isEmpty,
      blockers: blockers,
    );
  }

  /// The body of `POST …/send` and `POST …/accept`: only what was edited;
  /// empty when the suggestion goes out as it is.
  static Map<String, dynamic> toBody(QuoteEmailEdits? edits) => {
    if (edits?.sentTo case final to?) 'sentTo': to,
    if (edits?.cc case final cc?) 'cc': cc,
    if (edits?.subject case final subject?) 'subject': subject,
    if (edits?.message case final message?) 'message': message,
  };
}
