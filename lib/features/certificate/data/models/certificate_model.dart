import 'package:cloud_firestore/cloud_firestore.dart';

import '../../domain/certificate_template.dart';
import '../../domain/entities/certificate.dart';
import '../../domain/entities/certificate_result.dart';
import '../../domain/entities/checklist_answer.dart';

/// A `certificates/{requestId}` Firestore document.
class CertificateModel {
  final String requestId;
  final String inspectorId;
  final String templateId;
  final Map<String, String> texts;
  final Map<String, DateTime> dates;
  final Map<String, bool> answers;
  final ChecklistAnswer functionCheck;
  final CertificateResult? finalResult;
  final String? reviewNote;
  final DateTime? submittedAt;
  final String? reviewerComments;
  final String? reviewerName;
  final String? reviewerLicense;
  final String? sentTo;
  final DateTime? reviewedAt;
  final List<List<double>> signatureStrokes;
  final String? certificateNumber;
  final String? verificationCode;
  final String? qrData;
  final Map<String, String> issuedFor;

  const CertificateModel({
    required this.requestId,
    required this.inspectorId,
    this.templateId = CertificateTemplate.id,
    this.texts = const {},
    this.dates = const {},
    this.answers = const {},
    this.functionCheck = ChecklistAnswer.unanswered,
    this.finalResult,
    this.reviewNote,
    this.submittedAt,
    this.reviewerComments,
    this.reviewerName,
    this.reviewerLicense,
    this.sentTo,
    this.reviewedAt,
    this.signatureStrokes = const [],
    this.certificateNumber,
    this.verificationCode,
    this.qrData,
    this.issuedFor = const {},
  });

  factory CertificateModel.fromEntity(Certificate certificate) {
    return CertificateModel(
      requestId: certificate.requestId,
      inspectorId: certificate.inspectorId,
      templateId: certificate.templateId,
      texts: certificate.texts,
      dates: certificate.dates,
      answers: certificate.answers,
      functionCheck: certificate.functionCheck,
      finalResult: certificate.finalResult,
      reviewNote: certificate.reviewNote,
      submittedAt: certificate.submittedAt,
      reviewerComments: certificate.reviewerComments,
      reviewerName: certificate.reviewerName,
      reviewerLicense: certificate.reviewerLicense,
      sentTo: certificate.sentTo,
      reviewedAt: certificate.reviewedAt,
      signatureStrokes: certificate.signatureStrokes,
      certificateNumber: certificate.certificateNumber,
      verificationCode: certificate.verificationCode,
      qrData: certificate.qrData,
      issuedFor: certificate.issuedFor,
    );
  }

  factory CertificateModel.fromJson(String requestId, Map<String, dynamic> json) {
    final texts = <String, String>{};
    for (final entry in (json['texts'] as Map<String, dynamic>? ?? const {}).entries) {
      final value = entry.value;
      if (value is String && value.isNotEmpty) texts[entry.key] = value;
    }
    final dates = <String, DateTime>{};
    for (final entry in (json['dates'] as Map<String, dynamic>? ?? const {}).entries) {
      final value = entry.value;
      if (value is Timestamp) dates[entry.key] = value.toDate();
    }
    final answers = <String, bool>{};
    for (final entry in (json['answers'] as Map<String, dynamic>? ?? const {}).entries) {
      final value = entry.value;
      if (value is bool) answers[entry.key] = value;
    }
    return CertificateModel(
      requestId: requestId,
      inspectorId: json['inspectorId'] as String? ?? '',
      // Documents from before this template have none of the fields above,
      // but keep their template id so the form knows to start them afresh.
      templateId: json['templateId'] as String? ?? '',
      texts: texts,
      dates: dates,
      answers: answers,
      functionCheck: ChecklistAnswer.fromValue(json['functionCheck']),
      finalResult: CertificateResult.fromValue(json['finalResult']),
      reviewNote: json['reviewNote'] as String?,
      submittedAt: (json['submittedAt'] as Timestamp?)?.toDate(),
      reviewerComments: json['reviewerComments'] as String?,
      reviewerName: json['reviewerName'] as String?,
      reviewerLicense: json['reviewerLicense'] as String?,
      sentTo: json['sentTo'] as String?,
      reviewedAt: (json['reviewedAt'] as Timestamp?)?.toDate(),
      certificateNumber: json['certificateNumber'] as String?,
      verificationCode: json['verificationCode'] as String?,
      qrData: json['qrData'] as String?,
      issuedFor: {
        for (final entry in (json['issuedFor'] as Map<String, dynamic>? ?? const {}).entries)
          if (entry.value is String) entry.key: entry.value as String,
      },
      // Firestore can't nest arrays, so each stroke is stored as {'p': [...]}.
      signatureStrokes: [
        for (final stroke in (json['signature'] as List<dynamic>? ?? const []))
          [
            for (final v in ((stroke as Map<String, dynamic>)['p'] as List<dynamic>? ?? const []))
              (v as num).toDouble(),
          ],
      ],
    );
  }

  /// Just the inspector's own draft fields — never touches `submittedAt`
  /// (server-stamped only by [CertificateRemoteDataSource.submit]).
  ///
  /// The three maps always list every key — a cleared field goes out as
  /// null — because Firestore's merge-set would otherwise keep the old
  /// value of a key that's simply missing from the map.
  Map<String, dynamic> toJson() => {
    'inspectorId': inspectorId,
    'templateId': templateId,
    'texts': {for (final key in CertText.all) key: texts[key] ?? ''},
    'dates': {for (final key in CertDate.all) key: dates[key] == null ? null : Timestamp.fromDate(dates[key]!)},
    'answers': {for (final key in CertQuestion.all) key: answers[key]},
    'functionCheck': functionCheck.value,
    'finalResult': finalResult?.value,
    'reviewNote': reviewNote,
  };

  /// The technical manager's review fields (Feature 06) — what a review
  /// writes on top of the inspector's certificate. `reviewedAt` is
  /// server-stamped by [CertificateRemoteDataSource.saveReview].
  Map<String, dynamic> toReviewJson() => {
    'reviewNote': reviewNote,
    'reviewerComments': reviewerComments,
    'reviewerName': reviewerName,
    'reviewerLicense': reviewerLicense,
    'sentTo': sentTo,
    // Issued on approval; absent on a send-back, so a return never clears them.
    if (qrData != null) ...{
      'certificateNumber': certificateNumber,
      'verificationCode': verificationCode,
      'qrData': qrData,
      'issuedFor': issuedFor,
    },
    'signature': [
      for (final stroke in signatureStrokes) {'p': stroke},
    ],
  };

  Certificate toEntity() => Certificate(
    requestId: requestId,
    inspectorId: inspectorId,
    templateId: templateId,
    texts: texts,
    dates: dates,
    answers: answers,
    functionCheck: functionCheck,
    finalResult: finalResult,
    reviewNote: reviewNote,
    submittedAt: submittedAt,
    reviewerComments: reviewerComments,
    reviewerName: reviewerName,
    reviewerLicense: reviewerLicense,
    sentTo: sentTo,
    reviewedAt: reviewedAt,
    signatureStrokes: signatureStrokes,
    certificateNumber: certificateNumber,
    verificationCode: verificationCode,
    qrData: qrData,
    issuedFor: issuedFor,
  );
}
