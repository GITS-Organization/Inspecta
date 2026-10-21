import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/certificate/domain/entities/certificate.dart';
import 'package:inspecta/features/certificate/domain/repositories/certificate_repository.dart';
import 'package:inspecta/features/certificate/domain/usecases/approve_certificate.dart';
import 'package:inspecta/features/certificate/domain/usecases/return_certificate.dart';
import 'package:inspecta/features/certificate/domain/usecases/watch_certificate.dart';
import 'package:inspecta/features/technical_manager/domain/repositories/signature_repository.dart';
import 'package:inspecta/features/technical_manager/domain/usecases/save_signature.dart';
import 'package:inspecta/features/technical_manager/domain/usecases/watch_signature.dart';
import 'package:inspecta/features/technical_manager/presentation/bloc/review_detail_cubit.dart';

import '../requests/fake_requests_repository.dart';

class _FakeCertificateRepository extends Fake implements CertificateRepository {
  final certificate = StreamController<Certificate?>.broadcast();
  final approved = <Certificate>[];

  @override
  Stream<Certificate?> watchCertificate(String requestId) => certificate.stream;

  @override
  Future<void> approve(Certificate certificate) async => approved.add(certificate);
}

class _FakeSignatureRepository implements SignatureRepository {
  final signature = StreamController<List<List<double>>>.broadcast();
  final saves = <(String managerId, List<List<double>> strokes)>[];

  @override
  Stream<List<List<double>>> watchSignature(String managerId) => signature.stream;

  @override
  Future<void> saveSignature(String managerId, List<List<double>> strokes) async => saves.add((managerId, strokes));
}

const _drawn = [
  [0.1, 0.5, 0.4, 0.2, 0.7, 0.6],
];
const _saved = [
  [0.2, 0.8, 0.5, 0.3, 0.9, 0.7],
];

void main() {
  late _FakeCertificateRepository certificates;
  late _FakeSignatureRepository signatures;
  late ReviewDetailCubit cubit;

  Future<void> settle() => pumpEventQueue();

  /// A cubit whose certificate has loaded, for a manager named Mahmoud Nasr.
  Future<void> open({List<List<double>>? savedSignature, String managerId = 'tm-1'}) async {
    cubit = ReviewDetailCubit(
      request: sampleRequest(id: 'REQ-1'),
      managerId: managerId,
      reviewerName: 'Mahmoud Nasr',
      watchCertificate: WatchCertificate(certificates),
      approveCertificate: ApproveCertificate(certificates),
      returnCertificate: ReturnCertificate(certificates),
      watchSignature: WatchSignature(signatures),
      saveSignature: SaveSignature(signatures),
    )..start();
    certificates.certificate.add(const Certificate(requestId: 'REQ-1', inspectorId: 'insp-1'));
    if (savedSignature != null) signatures.signature.add(savedSignature);
    await settle();
  }

  setUp(() {
    certificates = _FakeCertificateRepository();
    signatures = _FakeSignatureRepository();
  });

  tearDown(() => cubit.close());

  group('with a signature saved in the profile', () {
    setUp(() async => open(savedSignature: _saved));

    test('can be approved without drawing anything', () {
      expect(cubit.state.hasSavedSignature, isTrue);
      expect(cubit.state.strokes, isEmpty);
      expect(cubit.state.canApprove, isTrue);
    });

    test('puts the saved signature, and the profile name, on the certificate', () async {
      await cubit.approve();

      final approved = certificates.approved.single;
      expect(approved.signatureStrokes, _saved);
      expect(approved.reviewerName, 'Mahmoud Nasr');
      expect(approved.reviewerLicense, isNull);
      expect(approved.sentTo, isNull);
    });

    test('issues the certificate number, a verification code and the QR text with the approval', () async {
      await cubit.approve();

      final approved = certificates.approved.single;
      expect(approved.certificateNumber, startsWith('CERT-'));
      expect(approved.verificationCode, matches(RegExp(r'^[A-Z2-9]{4}(-[A-Z2-9]{4}){2}$')));
      expect(approved.qrData, contains('Verification code: ${approved.verificationCode}'));
      expect(approved.qrData, contains('Certificate no: ${approved.certificateNumber}'));
    });

    test('uses the saved signature even if something was drawn on the pad', () async {
      cubit.setStrokes(_drawn);
      await cubit.approve();

      expect(certificates.approved.single.signatureStrokes, _saved);
    });

    test('does not save anything again', () async {
      await cubit.approve();
      await settle();

      expect(signatures.saves, isEmpty);
    });
  });

  group('with no saved signature', () {
    setUp(() async => open(savedSignature: const []));

    test('has to be signed first, and needs nothing but a signature and an address', () {
      expect(cubit.state.hasSavedSignature, isFalse);
      expect(cubit.state.canApprove, isFalse);

      cubit.setStrokes(_drawn);
      expect(cubit.state.canApprove, isTrue);
    });

    test('a name and a license are not asked for', () async {
      cubit.setStrokes(_drawn);
      await cubit.approve();

      expect(certificates.approved.single.reviewerName, 'Mahmoud Nasr');
      expect(certificates.approved.single.reviewerLicense, isNull);
    });

    test('saves what was drawn to the profile on approval, so it is not asked for again', () async {
      cubit.setStrokes(_drawn);
      await cubit.approve();
      await settle();

      expect(certificates.approved.single.signatureStrokes, _drawn);
      expect(signatures.saves, [('tm-1', _drawn)]);
    });

    test('keeps it to this one approval when asked not to remember it', () async {
      cubit.setStrokes(_drawn);
      cubit.setRememberSignature(false);
      await cubit.approve();
      await settle();

      expect(certificates.approved.single.signatureStrokes, _drawn);
      expect(signatures.saves, isEmpty);
    });

    test('is remembered by default', () {
      expect(cubit.state.rememberSignature, isTrue);
    });
  });

  test('cannot be approved before the saved signature has loaded', () async {
    await open();

    expect(cubit.state.isSignatureLoading, isTrue);
    cubit.setStrokes(_drawn);
    expect(cubit.state.canApprove, isFalse);

    signatures.signature.add(const []);
    await settle();
    expect(cubit.state.canApprove, isTrue);
  });

  test('a saved signature that cannot be read just means drawing one', () async {
    await open();
    signatures.signature.addError(Exception('permission-denied'));
    await settle();

    expect(cubit.state.isSignatureLoading, isFalse);
    expect(cubit.state.hasSavedSignature, isFalse);
  });

  test('has no saved signature to wait for without a signed-in manager', () async {
    await open(managerId: '');

    expect(cubit.state.isSignatureLoading, isFalse);
    expect(cubit.state.hasSavedSignature, isFalse);
  });

  test('a signature saved while the screen is open shows up straight away', () async {
    await open(savedSignature: const []);
    expect(cubit.state.hasSavedSignature, isFalse);

    signatures.signature.add(_saved);
    await settle();

    expect(cubit.state.hasSavedSignature, isTrue);
  });
}
