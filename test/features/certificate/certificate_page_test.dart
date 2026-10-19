import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/auth/domain/entities/qualification.dart';
import 'package:inspecta/features/auth/domain/entities/user.dart';
import 'package:inspecta/features/auth/domain/entities/user_role.dart';
import 'package:inspecta/features/certificate/domain/certificate_template.dart';
import 'package:inspecta/features/certificate/domain/entities/certificate.dart';
import 'package:inspecta/features/certificate/domain/entities/certificate_result.dart';
import 'package:inspecta/features/certificate/domain/entities/checklist_answer.dart';
import 'package:inspecta/features/certificate/domain/repositories/certificate_repository.dart';
import 'package:inspecta/features/certificate/domain/usecases/save_certificate_draft.dart';
import 'package:inspecta/features/certificate/domain/usecases/submit_certificate.dart';
import 'package:inspecta/features/certificate/domain/usecases/watch_certificate.dart';
import 'package:inspecta/features/certificate/presentation/bloc/certificate_cubit.dart';
import 'package:inspecta/features/certificate/presentation/pages/certificate_page.dart';
import 'package:inspecta/features/certificate/presentation/widgets/certificate_form_fields.dart';
import 'package:inspecta/features/requests/domain/entities/inspection_request.dart';
import 'package:inspecta/features/requests/domain/entities/request_item.dart';
import 'package:inspecta/injection.dart';
import 'package:inspecta/l10n/app_localizations.dart';

import '../requests/fake_requests_repository.dart';

class FakeCertificateRepository implements CertificateRepository {
  final controller = StreamController<Certificate?>.broadcast();
  Certificate? stored;
  Certificate? submitted;

  @override
  Stream<Certificate?> watchCertificate(String requestId) async* {
    yield stored;
    yield* controller.stream;
  }

  @override
  Stream<List<Certificate>> watchReviewedSince(DateTime since) => const Stream.empty();

  @override
  Future<void> saveDraft(Certificate certificate) async {
    stored = certificate;
    controller.add(certificate);
  }

  @override
  Future<void> submit(Certificate certificate) async {
    submitted = certificate;
    stored = certificate;
  }

  @override
  Future<void> approve(Certificate certificate) => throw UnimplementedError();

  @override
  Future<void> sendBack(Certificate certificate, {required String note}) => throw UnimplementedError();
}

void main() {
  late FakeCertificateRepository repo;
  late InspectionRequest request;

  const inspector = AppUser(
    id: 'insp-1',
    name: 'Anas Hegazi',
    email: 'anas@example.test',
    role: UserRole.inspector,
    qualifications: [
      Qualification(name: 'Compressors'),
      Qualification(name: 'Cranes'),
    ],
  );

  setUp(() {
    repo = FakeCertificateRepository();
    request = sampleRequest(
      id: 'REQ-1',
      items: const [
        RequestItem(
          id: 'item-1',
          type: 'Air Compressor',
          manufacturer: 'CATERPILLAR',
          model: 'C13',
          serialNumber: 'SN.: LGK06312',
          capacity: '10 Bar',
        ),
      ],
    );
    getIt.registerFactoryParam<CertificateCubit, InspectionRequest, void>(
      (request, _) => CertificateCubit(
        request: request,
        inspector: inspector,
        watchCertificate: WatchCertificate(repo),
        saveDraft: SaveCertificateDraft(repo),
        submitCertificate: SubmitCertificate(repo),
      ),
    );
    getIt.registerLazySingleton(() => WatchCertificate(repo));
  });

  tearDown(() async {
    await repo.controller.close();
    await getIt.reset();
  });

  Future<void> pumpPage(WidgetTester tester) async {
    // Tall enough that the whole form is built without scrolling.
    tester.view.physicalSize = const Size(390, 5200) * 2;
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MaterialApp(
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        locale: const Locale('en'),
        home: CertificatePage(request: request),
      ),
    );
    await tester.pumpAndSettle();
  }

  Finder yesNo(String question) => find.byWidgetPredicate((w) => w is CertificateYesNoInput && w.question == question);

  Finder textField(String key) => find.descendant(
    of: find.byWidgetPredicate((w) => w is CertificateTextInput && w.textKey == key),
    matching: find.byType(TextField),
  );

  Future<void> answer(WidgetTester tester, String question, String option) async {
    await tester.tap(find.descendant(of: yesNo(question), matching: find.text(option)));
    await tester.pumpAndSettle();
  }

  testWidgets('opens pre-filled from the request and today\'s date, and saves the new draft', (tester) async {
    await pumpPage(tester);

    expect(find.text('Report of thorough examination'), findsOneWidget);
    expect(find.text('Examination details'), findsWidgets);
    expect(find.text('Item information'), findsWidgets);
    expect(find.text('Examination questions'), findsWidgets);
    expect(find.text('Defects & remedial action'), findsWidgets);
    expect(find.text('Conclusion'), findsWidgets);

    // The item's own details, from the request.
    expect(tester.widget<TextField>(textField(CertText.manufacturer)).controller!.text, 'CATERPILLAR');
    expect(tester.widget<TextField>(textField(CertText.serialNumber)).controller!.text, 'SN.: LGK06312');
    expect(tester.widget<TextField>(textField(CertText.maxWorkingRate)).controller!.text, '10 Bar');
    expect(
      tester.widget<TextField>(textField(CertText.testType)).controller!.text,
      CertificateTemplate.defaultTestType,
    );

    // A brand-new draft was written, with today's examination date and
    // the inspector's name stamped on it.
    final draft = repo.stored!;
    expect(draft.templateId, CertificateTemplate.id);
    expect(draft.dateOf(CertDate.examination), isNotNull);
    expect(draft.dateOf(CertDate.nextExamination), isNotNull);
    expect(draft.text(CertText.preparedByName), 'Anas Hegazi');
    expect(draft.text(CertText.preparedByQualifications), 'Compressors, Cranes');
  });

  testWidgets('submit stays disabled until every section is filled, then submits', (tester) async {
    await pumpPage(tester);

    Finder submitButton() => find.widgetWithText(FilledButton, 'Submit to technical manager');
    expect(tester.widget<FilledButton>(submitButton()).onPressed, isNull);

    // Details: the standard of inspection is the one thing not pre-filled.
    await tester.enterText(textField(CertText.standardOfInspection), 'BS ISO 5389: 2005');
    // Item: the function check.
    await tester.tap(find.text('Pass'));
    await tester.pumpAndSettle();
    // Questions: "installed correctly" only appears after a first examination.
    await answer(tester, CertQuestion.firstExamination, 'Yes');
    expect(yesNo(CertQuestion.installedCorrectly), findsOneWidget);
    await answer(tester, CertQuestion.firstExamination, 'No');
    expect(yesNo(CertQuestion.installedCorrectly), findsNothing);
    for (final question in CertQuestion.carriedOut) {
      await answer(tester, question, 'No');
    }
    expect(tester.widget<FilledButton>(submitButton()).onPressed, isNull);

    // Defects are optional: nothing in that section was touched, and the
    // only thing still missing is the conclusion.
    expect(find.text('Optional — only if something was found'), findsOneWidget);
    expect(find.textContaining('(required)'), findsNothing);

    // Conclusion.
    expect(tester.widget<FilledButton>(submitButton()).onPressed, isNull);
    await tester.tap(find.text('Safe to operate'));
    await tester.pumpAndSettle();
    expect(tester.widget<FilledButton>(submitButton()).onPressed, isNotNull);

    await tester.tap(submitButton());
    await tester.pumpAndSettle();
    expect(find.text('Submit this certificate?'), findsOneWidget);
    await tester.tap(find.widgetWithText(TextButton, 'Submit'));
    await tester.pumpAndSettle();

    final submitted = repo.submitted!;
    expect(submitted.text(CertText.standardOfInspection), 'BS ISO 5389: 2005');
    expect(submitted.functionCheck, ChecklistAnswer.pass);
    expect(submitted.finalResult, CertificateResult.safeToOperate);
    expect(submitted.answerOf(CertQuestion.firstExamination), isFalse);
    expect(submitted.answerOf(CertQuestion.existingDanger), isNull);
    expect(submitted.answerOf(CertQuestion.futureDanger), isNull);
    expect(submitted.text(CertText.preparedByName), 'Anas Hegazi');
    expect(submitted.isReadyToSubmit, isTrue);
  });

  test('the 6-month and 12-month interval answers are always opposites', () async {
    final cubit = CertificateCubit(
      request: request,
      inspector: inspector,
      watchCertificate: WatchCertificate(repo),
      saveDraft: SaveCertificateDraft(repo),
      submitCertificate: SubmitCertificate(repo),
    )..start();
    Future<void> settle() => Future<void>.delayed(Duration.zero);
    bool? answer(String question) => cubit.state.certificate!.answerOf(question);
    await settle();

    cubit.setAnswer(CertQuestion.within6Months, true);
    await settle();
    expect(answer(CertQuestion.within6Months), isTrue);
    expect(answer(CertQuestion.within12Months), isFalse);

    cubit.setAnswer(CertQuestion.within12Months, true);
    await settle();
    expect(answer(CertQuestion.within12Months), isTrue);
    expect(answer(CertQuestion.within6Months), isFalse);

    // And the other way round: a no on one is a yes on the other.
    cubit.setAnswer(CertQuestion.within12Months, false);
    await settle();
    expect(answer(CertQuestion.within6Months), isTrue);
    cubit.setAnswer(CertQuestion.within6Months, false);
    await settle();
    expect(answer(CertQuestion.within12Months), isTrue);

    // The other two questions in that block are independent.
    cubit.setAnswer(CertQuestion.examinationScheme, true);
    await settle();
    expect(answer(CertQuestion.within12Months), isTrue);
    expect(answer(CertQuestion.exceptionalCircumstances), isNull);

    // What was picked is what was saved.
    expect(repo.stored!.answerOf(CertQuestion.within6Months), isFalse);
    expect(repo.stored!.answerOf(CertQuestion.within12Months), isTrue);

    await cubit.close();
  });

  test("moving the examination date moves the suggested next date with it, unless it was changed", () async {
    CertificateCubit build() => CertificateCubit(
      request: request,
      inspector: inspector,
      watchCertificate: WatchCertificate(repo),
      saveDraft: SaveCertificateDraft(repo),
      submitCertificate: SubmitCertificate(repo),
    );
    Future<void> settle() => Future<void>.delayed(Duration.zero);

    final cubit = build()..start();
    await settle();
    cubit.setDate(CertDate.examination, DateTime(2026, 9, 24));
    await settle();
    expect(cubit.state.certificate!.dateOf(CertDate.nextExamination), DateTime(2027, 3, 23));

    // Still the suggestion, so it follows.
    cubit.setDate(CertDate.examination, DateTime(2026, 10, 1));
    await settle();
    expect(cubit.state.certificate!.dateOf(CertDate.nextExamination), DateTime(2027, 3, 31));

    // The inspector picks their own next date — it now stays put.
    cubit.setDate(CertDate.nextExamination, DateTime(2027, 9, 30));
    cubit.setDate(CertDate.examination, DateTime(2026, 11, 1));
    await settle();
    expect(cubit.state.certificate!.dateOf(CertDate.nextExamination), DateTime(2027, 9, 30));

    await cubit.close();
  });
}
