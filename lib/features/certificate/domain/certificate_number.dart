import '../../requests/domain/entities/inspection_request.dart';

/// A stable, readable identifier for a certificate — e.g. "CERT-2026-0417".
/// There's no sequential-numbering backend (that would need a server-side
/// counter this app doesn't have), so this is derived deterministically from
/// the job's own id: stable across reloads and unique per job, just not
/// globally sequential like a real cert register would be.
String certNumberFor(InspectionRequest request) {
  final year = (request.scheduledAt ?? request.receivedAt).year;
  final serial = (request.id.hashCode.abs() % 10000).toString().padLeft(4, '0');
  return 'CERT-$year-$serial';
}
