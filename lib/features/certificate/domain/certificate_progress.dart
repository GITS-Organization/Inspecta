import 'entities/certificate.dart';

/// The certificate form's 5 sections, in order — used for the "Step N of
/// 5" header on the form and the draft progress bar on the Certificates
/// list (Feature 05). [defects] is optional, so it is never the current
/// step: the form goes from the questions straight to the conclusion.
enum CertificateStep { details, item, questions, defects, conclusion }

/// Pure progress helpers shared by the certificate form (its "Step N of
/// 5" header) and the Certificates list (a draft's "40%" progress bar) —
/// works on a possibly-not-yet-created certificate, since a job's
/// certificate doc doesn't exist until the inspector's first save.
extension CertificateProgress on Certificate? {
  /// The first required section that isn't done yet.
  CertificateStep get currentStep {
    final c = this;
    if (c == null || !c.isDetailsComplete) return CertificateStep.details;
    if (!c.isItemComplete) return CertificateStep.item;
    if (!c.isQuestionsComplete) return CertificateStep.questions;
    return CertificateStep.conclusion;
  }

  int get currentStepNumber => CertificateStep.values.indexOf(currentStep) + 1;

  int get totalSteps => CertificateStep.values.length;

  int get percentComplete => (currentStepNumber / totalSteps * 100).round();
}
