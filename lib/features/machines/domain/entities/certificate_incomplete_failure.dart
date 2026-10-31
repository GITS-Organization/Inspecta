import '../../../../core/error/failure.dart';

/// The API refused a submit (409 `certificate_incomplete`) because the
/// machine's certificate is not finished; [problems] has one entry per missing
/// item, worded for the inspector.
class CertificateIncompleteFailure extends Failure {
  final List<String> problems;

  const CertificateIncompleteFailure(this.problems);

  @override
  String toString() => 'CertificateIncompleteFailure(${problems.length} problems)';
}
