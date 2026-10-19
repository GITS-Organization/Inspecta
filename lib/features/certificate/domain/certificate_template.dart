/// The inspection certificate is TÜV Austria Egypt's one-page "Report of
/// Thorough Examination": a client/dates block, the item's identification
/// and function check, six yes/no examination questions, the defects
/// found (and what's needed to remedy them) and the conclusion. Every
/// job uses this one template, whatever the equipment type.
class CertificateTemplate {
  CertificateTemplate._();

  static const String id = 'TUV-RTE-01';

  /// What the "Test type" field starts as — the inspector can change it.
  static const String defaultTestType = 'Visual & function test';

  /// How long after an examination the next one is suggested.
  static const int defaultIntervalMonths = 6;

  /// The next examination date to suggest: [defaultIntervalMonths] after
  /// [examination], less a day — 24/09/2026 → 23/03/2027, as on the paper
  /// certificate.
  static DateTime suggestedNextExamination(DateTime examination) {
    final targetMonth = examination.month + defaultIntervalMonths;
    final lastDayOfTarget = DateTime(examination.year, targetMonth + 1, 0).day;
    final day = examination.day > lastDayOfTarget ? lastDayOfTarget : examination.day;
    return DateTime(examination.year, targetMonth, day).subtract(const Duration(days: 1));
  }
}

/// Keys of the certificate's free-text fields.
abstract final class CertText {
  static const clientRepresentative = 'clientRepresentative';
  static const standardOfInspection = 'standardOfInspection';
  static const testType = 'testType';

  static const manufacturer = 'manufacturer';
  static const modelYear = 'modelYear';
  static const maxWorkingRate = 'maxWorkingRate';
  static const serialNumber = 'serialNumber';
  static const ownerId = 'ownerId';
  static const ndt = 'ndt';

  static const defectDescription = 'defectDescription';
  static const repairsRequired = 'repairsRequired';
  static const testsCarriedOut = 'testsCarriedOut';
  static const note = 'note';

  /// "Name & qualifications of person making this report" — snapshotted from
  /// the inspector's profile when they submit.
  static const preparedByName = 'preparedByName';
  static const preparedByQualifications = 'preparedByQualifications';

  static const all = [
    clientRepresentative,
    standardOfInspection,
    testType,
    manufacturer,
    modelYear,
    maxWorkingRate,
    serialNumber,
    ownerId,
    ndt,
    defectDescription,
    repairsRequired,
    testsCarriedOut,
    note,
    preparedByName,
    preparedByQualifications,
  ];
}

/// Keys of the certificate's date fields.
abstract final class CertDate {
  static const examination = 'examination';
  static const nextExamination = 'nextExamination';

  /// "If YES state the date by when" — the defect that isn't a danger yet.
  static const futureDangerBy = 'futureDangerBy';

  static const all = [examination, nextExamination, futureDangerBy];
}

/// Keys of the certificate's yes/no questions.
abstract final class CertQuestion {
  static const firstExamination = 'firstExamination';
  static const installedCorrectly = 'installedCorrectly';

  static const within6Months = 'within6Months';
  static const within12Months = 'within12Months';
  static const examinationScheme = 'examinationScheme';
  static const exceptionalCircumstances = 'exceptionalCircumstances';

  static const existingDanger = 'existingDanger';
  static const futureDanger = 'futureDanger';

  /// The paper form asks about the examination interval twice — "within 6
  /// months?" and "within 12 months?" — but only one interval applies, so the
  /// two answers are always opposites: yes to one is no to the other. Returns
  /// the question that answers [question] the opposite way, or null when
  /// [question] isn't one of the pair.
  static String? intervalCounterpart(String question) => switch (question) {
    within6Months => within12Months,
    within12Months => within6Months,
    _ => null,
  };

  /// "Was the examination carried out…" — the four that sit under that
  /// heading on the paper certificate, in order.
  static const carriedOut = [within6Months, within12Months, examinationScheme, exceptionalCircumstances];

  static const all = [firstExamination, installedCorrectly, ...carriedOut, existingDanger, futureDanger];
}
