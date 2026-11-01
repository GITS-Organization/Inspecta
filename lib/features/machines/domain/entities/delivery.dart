import 'package:equatable/equatable.dart';

/// One certificate the delivery email carries, as the preview lists it.
class DeliveryCertificate extends Equatable {
  final String? requestItemId;
  final String number;
  final String? name;

  /// `satisfactory` or `unsatisfactory`.
  final String? result;
  final String? examinationDate;
  final String? nextExaminationDate;

  /// The public page where the client can check the certificate.
  final String? checkUrl;

  /// The API path of the certificate's PDF
  /// (`/quotation-queue/{id}/machines/{itemId}/pdf`).
  final String? pdfUrl;

  const DeliveryCertificate({
    required this.number,
    this.requestItemId,
    this.name,
    this.result,
    this.examinationDate,
    this.nextExaminationDate,
    this.checkUrl,
    this.pdfUrl,
  });

  @override
  List<Object?> get props => [
    requestItemId,
    number,
    name,
    result,
    examinationDate,
    nextExaminationDate,
    checkUrl,
    pdfUrl,
  ];
}

/// The email that would go to the client with the job's certificates
/// (`GET …/deliver-to-client/preview`): ready to send as it is, or to edit
/// first — who it's for, what it says and the certificates it carries.
class DeliveryPreview extends Equatable {
  final String? to;
  final List<String> cc;
  final String subject;
  final String body;
  final List<DeliveryCertificate> certificates;

  /// False while something stops the email from going out; [blockers] says
  /// what.
  final bool canSend;
  final List<String> blockers;

  const DeliveryPreview({
    this.to,
    this.cc = const [],
    this.subject = '',
    this.body = '',
    this.certificates = const [],
    this.canSend = false,
    this.blockers = const [],
  });

  @override
  List<Object?> get props => [to, cc, subject, body, certificates, canSend, blockers];
}

/// What went out when a job was delivered (`POST …/deliver-to-client`, or the
/// `delivery` of the machines answer afterwards). [emailSent] is false when
/// the job was handed over without an email.
class DeliveryResult extends Equatable {
  final bool emailSent;
  final String? sentTo;
  final List<String> cc;
  final String? subject;
  final DateTime? sentAt;

  const DeliveryResult({
    this.emailSent = false,
    this.sentTo,
    this.cc = const [],
    this.subject,
    this.sentAt,
  });

  @override
  List<Object?> get props => [emailSent, sentTo, cc, subject, sentAt];
}
