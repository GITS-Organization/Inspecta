import 'package:equatable/equatable.dart';

/// The email that would go to the client with the job's certificates
/// (`GET …/deliver-to-client/preview`): who it's for, what it says and which
/// files it carries.
class DeliveryPreview extends Equatable {
  final String? to;
  final String subject;
  final String text;

  /// The attached files' names.
  final List<String> attachments;

  const DeliveryPreview({this.to, this.subject = '', this.text = '', this.attachments = const []});

  @override
  List<Object?> get props => [to, subject, text, attachments];
}

/// How a delivery ended (`POST …/deliver-to-client`): the job is handed
/// over, but sending the email is switched off on the API for now, so
/// [emailSent] is false until it's turned on.
class DeliveryResult extends Equatable {
  final bool emailSent;

  const DeliveryResult({this.emailSent = false});

  @override
  List<Object?> get props => [emailSent];
}
