import 'package:equatable/equatable.dart';

/// A file attached to a client's reply. Only its name, type and size are kept:
/// the API does not store the content.
class ReplyAttachment extends Equatable {
  final String fileName;
  final String contentType;
  final int sizeBytes;

  const ReplyAttachment({required this.fileName, this.contentType = '', this.sizeBytes = 0});

  @override
  List<Object?> get props => [fileName, contentType, sizeBytes];
}

/// An email the client sent in answer to a quotation. Replies are kept with
/// the quotation they answered, not turned into requests.
///
/// Only [text] — what the client wrote, without the quoted messages and the
/// signature — is shown. The API also sends the email's HTML as it arrived,
/// which can't be trusted, so it is not read.
class ClientReply extends Equatable {
  final String id;
  final String quoteId;
  final String fromAddress;
  final String fromName;
  final String subject;
  final DateTime? receivedAt;
  final String text;
  final List<ReplyAttachment> attachments;

  const ClientReply({
    required this.id,
    required this.quoteId,
    required this.fromAddress,
    required this.fromName,
    required this.subject,
    required this.text,
    this.receivedAt,
    this.attachments = const [],
  });

  /// Who sent it: the name when there is one, else the address.
  String get sender => fromName.trim().isNotEmpty ? fromName.trim() : fromAddress;

  @override
  List<Object?> get props => [id, quoteId, fromAddress, fromName, subject, receivedAt, text, attachments];
}
