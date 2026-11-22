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

  /// The PDF's size, counted against the email's size limit.
  final int sizeBytes;

  const DeliveryCertificate({
    required this.number,
    this.requestItemId,
    this.name,
    this.result,
    this.examinationDate,
    this.nextExaminationDate,
    this.checkUrl,
    this.pdfUrl,
    this.sizeBytes = 0,
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
    sizeBytes,
  ];
}

/// One of the inspector's report files the delivery email carries, as the
/// preview lists it. The coordinator can untick it to leave it out.
class DeliveryReport extends Equatable {
  final String id;

  /// The name it will have in the email.
  final String fileName;
  final int sizeBytes;

  /// Null for the job as a whole.
  final String? machineName;

  const DeliveryReport({required this.id, required this.fileName, required this.sizeBytes, this.machineName});

  @override
  List<Object?> get props => [id, fileName, sizeBytes, machineName];
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

  /// The inspector's reports, all ticked to go unless the coordinator unticks
  /// them; empty when the job has none.
  final List<DeliveryReport> reports;

  /// The most the email may weigh when it carries reports: the certificate
  /// PDFs plus the ticked reports, in raw bytes (17 MB by default).
  final int maxEmailBytes;

  /// What the API says when the certificates and every report together are
  /// over [maxEmailBytes]. It is a warning the coordinator clears by
  /// unticking, not a reason to stop.
  static const reportsBlockerMarker = 'Untick some reports';

  static const defaultMaxEmailBytes = 17825792;

  const DeliveryPreview({
    this.to,
    this.cc = const [],
    this.subject = '',
    this.body = '',
    this.certificates = const [],
    this.canSend = false,
    this.blockers = const [],
    this.reports = const [],
    this.maxEmailBytes = defaultMaxEmailBytes,
  });

  /// The blockers that unticking reports clears.
  List<String> get reportBlockers => [
    for (final blocker in blockers)
      if (blocker.contains(reportsBlockerMarker)) blocker,
  ];

  /// The blockers that stay whatever is ticked.
  List<String> get hardBlockers => [
    for (final blocker in blockers)
      if (!blocker.contains(reportsBlockerMarker)) blocker,
  ];

  @override
  List<Object?> get props => [
    to,
    cc,
    subject,
    body,
    certificates,
    canSend,
    blockers,
    reports,
    maxEmailBytes,
  ];
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
