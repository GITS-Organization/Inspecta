import 'package:equatable/equatable.dart';

import '../certificate_template.dart';
import 'certificate_result.dart';
import 'checklist_answer.dart';

/// A `certificates/{requestId}` document (Feature 05): one per job, owned
/// by the inspector it's assigned to. Autosaved as the inspector fills it
/// in, locked once the job's [JobStatus] moves past `inProgress`.
///
/// It's TÜV's "Report of Thorough Examination" (see [CertificateTemplate]):
/// the free-text, date and yes/no fields live in maps keyed by
/// [CertText] / [CertDate] / [CertQuestion], so the form and the PDF can
/// both walk the same keys.
class Certificate extends Equatable {
  final String requestId;
  final String inspectorId;
  final String templateId;

  final Map<String, String> texts;
  final Map<String, DateTime> dates;
  final Map<String, bool> answers;

  /// The item's "Function check" row.
  final ChecklistAnswer functionCheck;

  /// The "Conclusion / remarks" row.
  final CertificateResult? finalResult;

  /// Set by a technical manager sending a certificate back for changes;
  /// the Certificates list displays it, and the form shows it as a banner.
  final String? reviewNote;

  /// Server-stamped the moment the inspector submits (or resubmits) —
  /// never set by the generic autosave. Powers the Certificates list's
  /// "Submitted N ago".
  final DateTime? submittedAt;

  /// The technical manager's review (Feature 06) — filled in when they
  /// approve a submitted certificate. All empty until then; cleared again
  /// when the inspector resubmits after a return.
  final String? reviewerComments;
  final String? reviewerName;
  final String? reviewerLicense;

  /// Where the signed PDF was addressed when approved.
  final String? sentTo;
  final DateTime? reviewedAt;

  /// The reviewer's drawn signature: one flat `[x0, y0, x1, y1, ...]` list
  /// per pen stroke, each coordinate normalised to 0..1 of the pad so it
  /// redraws at any size.
  final List<List<double>> signatureStrokes;

  /// Issued when the technical manager approves: the certificate's number,
  /// a random code that identifies it, and the exact text its QR code
  /// carries. Stored so the PDF and the management dashboard show the same
  /// QR. Null until approval.
  final String? certificateNumber;
  final String? verificationCode;
  final String? qrData;

  const Certificate({
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
  });

  bool get isSigned => signatureStrokes.any((stroke) => stroke.length >= 4);

  /// The trimmed text of [key], or null when it's empty.
  String? text(String key) {
    final value = texts[key]?.trim();
    return value == null || value.isEmpty ? null : value;
  }

  DateTime? dateOf(String key) => dates[key];

  /// `null` until the inspector has answered that question.
  bool? answerOf(String key) => answers[key];

  /// The recorded defect, for the reviewer's result banner.
  String? get defectSummary => text(CertText.defectDescription);

  bool get isDetailsComplete =>
      dateOf(CertDate.examination) != null &&
      dateOf(CertDate.nextExamination) != null &&
      text(CertText.standardOfInspection) != null &&
      text(CertText.testType) != null;

  bool get isItemComplete => functionCheck != ChecklistAnswer.unanswered;

  /// Every yes/no question answered — "installed correctly" only counts
  /// when this was a first examination, since the paper form only asks it
  /// then.
  bool get isQuestionsComplete {
    final first = answerOf(CertQuestion.firstExamination);
    if (first == null) return false;
    if (first && answerOf(CertQuestion.installedCorrectly) == null) return false;
    return CertQuestion.carriedOut.every((key) => answerOf(key) != null);
  }

  bool get isConclusionComplete => finalResult != null;

  /// The defects section has no completeness rule: nothing in it is
  /// required, so it never holds up a submission.
  bool get isReadyToSubmit => isDetailsComplete && isItemComplete && isQuestionsComplete && isConclusionComplete;

  Certificate copyWith({
    String? templateId,
    Map<String, String>? texts,
    Map<String, DateTime>? dates,
    Map<String, bool>? answers,
    ChecklistAnswer? functionCheck,
    CertificateResult? finalResult,
  }) {
    return Certificate(
      requestId: requestId,
      inspectorId: inspectorId,
      templateId: templateId ?? this.templateId,
      texts: texts ?? this.texts,
      dates: dates ?? this.dates,
      answers: answers ?? this.answers,
      functionCheck: functionCheck ?? this.functionCheck,
      finalResult: finalResult ?? this.finalResult,
      // Not settable here — reviewNote is a technical manager's, and
      // submittedAt is server-stamped on submit; every field this
      // copyWith touches is the inspector's own draft data.
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
    );
  }

  Certificate withText(String key, String value) => copyWith(texts: {...texts, key: value});

  /// A null [value] clears the date.
  Certificate withDate(String key, DateTime? value) {
    final next = {...dates};
    if (value == null) {
      next.remove(key);
    } else {
      next[key] = value;
    }
    return copyWith(dates: next);
  }

  Certificate withAnswer(String key, bool value) => copyWith(answers: {...answers, key: value});

  /// The certificate as the technical manager leaves it — only the review
  /// fields change; everything the inspector filled in stays as is.
  Certificate withReview({
    String? reviewNote,
    String? reviewerComments,
    String? reviewerName,
    String? reviewerLicense,
    String? sentTo,
    List<List<double>>? signatureStrokes,
    String? certificateNumber,
    String? verificationCode,
    String? qrData,
  }) {
    return Certificate(
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
      signatureStrokes: signatureStrokes ?? const [],
      certificateNumber: certificateNumber ?? this.certificateNumber,
      verificationCode: verificationCode ?? this.verificationCode,
      qrData: qrData ?? this.qrData,
    );
  }

  @override
  List<Object?> get props => [
    requestId,
    inspectorId,
    templateId,
    texts,
    dates,
    answers,
    functionCheck,
    finalResult,
    reviewNote,
    submittedAt,
    reviewerComments,
    reviewerName,
    reviewerLicense,
    sentTo,
    reviewedAt,
    signatureStrokes,
    certificateNumber,
    verificationCode,
    qrData,
  ];
}
