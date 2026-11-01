import 'package:equatable/equatable.dart';

/// The email that would carry a quotation's PDF to the client
/// (`GET …/quotes/{id}/email`): ready to send as it is, or to edit first.
/// Nothing in it has been sent or saved.
class QuoteEmailPreview extends Equatable {
  /// The sender of the original request; null when there's no address yet.
  final String? to;
  final List<String> cc;
  final String subject;

  /// The text the coordinator edits. The signature and the quoted original
  /// email are added automatically when it goes out.
  final String body;

  /// The coordinator's mailbox the email is sent from.
  final String? from;

  /// The attached PDF's file name.
  final String? attachmentName;

  /// False while emailing is switched off on the API: Send then only changes
  /// the quotation.
  final bool emailEnabled;

  /// An extra line from the API about this email, when it has one.
  final String? notice;

  /// False while something stops the email from going out; [blockers] says
  /// what (a price missing, no mailbox, no address).
  final bool canSend;
  final List<String> blockers;

  const QuoteEmailPreview({
    this.to,
    this.cc = const [],
    this.subject = '',
    this.body = '',
    this.from,
    this.attachmentName,
    this.emailEnabled = true,
    this.notice,
    this.canSend = false,
    this.blockers = const [],
  });

  @override
  List<Object?> get props => [
    to,
    cc,
    subject,
    body,
    from,
    attachmentName,
    emailEnabled,
    notice,
    canSend,
    blockers,
  ];
}

/// The email as it goes out with Send or Accept: the body of the request.
/// The API takes each part given instead of its own suggestion; a part left
/// null (no address, say) keeps the suggestion.
class QuoteEmailEdits extends Equatable {
  final String? sentTo;
  final List<String>? cc;
  final String? subject;

  /// The whole text of the email.
  final String? message;

  const QuoteEmailEdits({this.sentTo, this.cc, this.subject, this.message});

  /// Nothing was changed: the suggestion goes out as it is.
  bool get isEmpty => sentTo == null && cc == null && subject == null && message == null;

  @override
  List<Object?> get props => [sentTo, cc, subject, message];
}
