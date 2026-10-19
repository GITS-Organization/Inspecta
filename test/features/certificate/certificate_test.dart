import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/certificate/data/models/certificate_model.dart';
import 'package:inspecta/features/certificate/domain/certificate_defaults.dart';
import 'package:inspecta/features/certificate/domain/certificate_progress.dart';
import 'package:inspecta/features/certificate/domain/certificate_template.dart';
import 'package:inspecta/features/certificate/domain/entities/certificate.dart';
import 'package:inspecta/features/certificate/domain/entities/certificate_result.dart';
import 'package:inspecta/features/certificate/domain/entities/checklist_answer.dart';
import 'package:inspecta/features/requests/domain/entities/request_item.dart';

import '../requests/fake_requests_repository.dart';

/// A certificate with every required field filled in.
Certificate completeCertificate() {
  var c = const Certificate(requestId: 'REQ-1', inspectorId: 'insp-1');
  c = c
      .withDate(CertDate.examination, DateTime(2026, 9, 24))
      .withDate(CertDate.nextExamination, DateTime(2027, 3, 23))
      .withText(CertText.standardOfInspection, 'BS ISO 5389: 2005')
      .withText(CertText.testType, 'Visual & function test')
      .copyWith(functionCheck: ChecklistAnswer.pass, finalResult: CertificateResult.safeToOperate);
  for (final key in [CertQuestion.firstExamination, CertQuestion.existingDanger, CertQuestion.futureDanger]) {
    c = c.withAnswer(key, false);
  }
  for (final key in CertQuestion.carriedOut) {
    c = c.withAnswer(key, key == CertQuestion.within6Months);
  }
  return c;
}

void main() {
  group('CertificateTemplate.suggestedNextExamination', () {
    test('is six months on, less a day', () {
      expect(CertificateTemplate.suggestedNextExamination(DateTime(2026, 9, 24)), DateTime(2027, 3, 23));
    });

    test('clamps to the end of a shorter month', () {
      expect(CertificateTemplate.suggestedNextExamination(DateTime(2026, 8, 31)), DateTime(2027, 2, 27));
    });

    test('rolls over the year', () {
      expect(CertificateTemplate.suggestedNextExamination(DateTime(2026, 12, 1)), DateTime(2027, 5, 31));
    });
  });

  group('CertQuestion.intervalCounterpart', () {
    test('pairs the 6-month and 12-month questions with each other', () {
      expect(CertQuestion.intervalCounterpart(CertQuestion.within6Months), CertQuestion.within12Months);
      expect(CertQuestion.intervalCounterpart(CertQuestion.within12Months), CertQuestion.within6Months);
    });

    test('has none for any other question', () {
      for (final key in CertQuestion.all.where((q) => q != CertQuestion.within6Months && q != CertQuestion.within12Months)) {
        expect(CertQuestion.intervalCounterpart(key), isNull, reason: key);
      }
    });
  });

  group('Certificate completeness', () {
    test('a fully filled certificate is ready to submit', () {
      expect(completeCertificate().isReadyToSubmit, isTrue);
    });

    test('an empty certificate is on the first step with nothing done', () {
      const empty = Certificate(requestId: 'REQ-1', inspectorId: 'insp-1');
      expect(empty.isReadyToSubmit, isFalse);
      expect(empty.currentStep, CertificateStep.details);
      expect(empty.percentComplete, 20);
    });

    test('the step moves on as each section is finished', () {
      var c = const Certificate(requestId: 'REQ-1', inspectorId: 'insp-1');
      final full = completeCertificate();

      c = c.copyWith(dates: full.dates, texts: full.texts);
      expect(c.currentStep, CertificateStep.item);

      c = c.copyWith(functionCheck: ChecklistAnswer.pass);
      expect(c.currentStep, CertificateStep.questions);

      c = c.copyWith(answers: full.answers);
      expect(c.currentStep, CertificateStep.conclusion);
      expect(c.percentComplete, 100);
    });

    test('blank text does not count as filled in', () {
      final c = completeCertificate().withText(CertText.testType, '   ');
      expect(c.isDetailsComplete, isFalse);
    });

    test('"installed correctly" is only required after a first examination', () {
      var c = completeCertificate();
      expect(c.isQuestionsComplete, isTrue);

      c = c.withAnswer(CertQuestion.firstExamination, true);
      expect(c.isQuestionsComplete, isFalse);

      c = c.withAnswer(CertQuestion.installedCorrectly, true);
      expect(c.isQuestionsComplete, isTrue);
    });

    test('nothing in the defects section is required', () {
      // Not even the two danger questions have been answered.
      final c = Certificate(
        requestId: 'REQ-1',
        inspectorId: 'insp-1',
        texts: completeCertificate().texts,
        dates: completeCertificate().dates,
        answers: {
          for (final answer in completeCertificate().answers.entries)
            if (answer.key != CertQuestion.existingDanger && answer.key != CertQuestion.futureDanger)
              answer.key: answer.value,
        },
        functionCheck: ChecklistAnswer.pass,
        finalResult: CertificateResult.safeToOperate,
      );
      expect(c.answerOf(CertQuestion.existingDanger), isNull);
      expect(c.defectSummary, isNull);
      expect(c.isReadyToSubmit, isTrue);
    });

    test('a recorded defect, with or without its details, does not hold up submission', () {
      var c = completeCertificate().withAnswer(CertQuestion.existingDanger, true);
      expect(c.isReadyToSubmit, isTrue);

      c = c.withAnswer(CertQuestion.futureDanger, true);
      expect(c.isReadyToSubmit, isTrue);

      c = c.withText(CertText.defectDescription, 'Cracked pressure relief valve');
      expect(c.defectSummary, 'Cracked pressure relief valve');
      expect(c.isReadyToSubmit, isTrue);
    });

    test('the conclusion is the last thing required', () {
      final c = Certificate(
        requestId: completeCertificate().requestId,
        inspectorId: 'insp-1',
        texts: completeCertificate().texts,
        dates: completeCertificate().dates,
        answers: completeCertificate().answers,
        functionCheck: ChecklistAnswer.pass,
      );
      expect(c.isConclusionComplete, isFalse);
      expect(c.isReadyToSubmit, isFalse);
    });

    test('clearing a date removes it', () {
      final c = completeCertificate().withDate(CertDate.futureDangerBy, DateTime(2026, 12, 1));
      expect(c.dateOf(CertDate.futureDangerBy), isNotNull);
      expect(c.withDate(CertDate.futureDangerBy, null).dateOf(CertDate.futureDangerBy), isNull);
    });
  });

  group('seedCertificate', () {
    final request = sampleRequest(
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

    test('fills in what the job already knows, and today\'s dates', () {
      const blank = Certificate(requestId: 'REQ-1', inspectorId: 'insp-1');
      final seeded = seedCertificate(blank, request: request, now: DateTime(2026, 9, 24, 13, 30));

      expect(seeded.text(CertText.manufacturer), 'CATERPILLAR');
      expect(seeded.text(CertText.modelYear), 'C13');
      expect(seeded.text(CertText.serialNumber), 'SN.: LGK06312');
      expect(seeded.text(CertText.maxWorkingRate), '10 Bar');
      expect(seeded.text(CertText.testType), CertificateTemplate.defaultTestType);
      expect(seeded.dateOf(CertDate.examination), DateTime(2026, 9, 24));
      expect(seeded.dateOf(CertDate.nextExamination), DateTime(2027, 3, 23));
      expect(seeded.templateId, CertificateTemplate.id);
    });

    test('never overwrites what the inspector already entered', () {
      final blank = const Certificate(
        requestId: 'REQ-1',
        inspectorId: 'insp-1',
      ).withText(CertText.manufacturer, 'Atlas Copco').withDate(CertDate.examination, DateTime(2026, 1, 5));
      final seeded = seedCertificate(blank, request: request, now: DateTime(2026, 9, 24));

      expect(seeded.text(CertText.manufacturer), 'Atlas Copco');
      expect(seeded.dateOf(CertDate.examination), DateTime(2026, 1, 5));
      // …and so doesn't invent a next date for it either.
      expect(seeded.dateOf(CertDate.nextExamination), isNull);
    });
  });

  group('CertificateModel', () {
    test('a cleared field is written as empty/null so a merge-set overwrites it', () {
      final model = CertificateModel.fromEntity(const Certificate(requestId: 'REQ-1', inspectorId: 'insp-1'));
      final json = model.toJson();

      expect((json['texts'] as Map).keys, containsAll(CertText.all));
      expect((json['texts'] as Map)[CertText.note], '');
      expect((json['dates'] as Map).keys, containsAll(CertDate.all));
      expect((json['dates'] as Map)[CertDate.futureDangerBy], isNull);
      expect((json['answers'] as Map).keys, containsAll(CertQuestion.all));
      expect((json['answers'] as Map)[CertQuestion.firstExamination], isNull);
    });

    test('survives a round trip through its JSON', () {
      final original = completeCertificate()
          .withText(CertText.manufacturer, 'CATERPILLAR')
          .withDate(CertDate.futureDangerBy, DateTime(2026, 12, 1));
      final json = CertificateModel.fromEntity(original).toJson();

      final restored = CertificateModel.fromJson('REQ-1', json).toEntity();

      expect(restored.texts[CertText.manufacturer], 'CATERPILLAR');
      expect(restored.dateOf(CertDate.examination), DateTime(2026, 9, 24));
      expect(restored.dateOf(CertDate.futureDangerBy), DateTime(2026, 12, 1));
      expect(restored.answerOf(CertQuestion.within6Months), isTrue);
      expect(restored.answerOf(CertQuestion.within12Months), isFalse);
      expect(restored.functionCheck, ChecklistAnswer.pass);
      expect(restored.finalResult, CertificateResult.safeToOperate);
      expect(restored.isReadyToSubmit, isTrue);
    });

    test('a document from the old lift template loads empty, on its old template id', () {
      final legacy = CertificateModel.fromJson('REQ-1', {
        'inspectorId': 'insp-1',
        'templateId': 'CI-LIFT-01',
        'checklistAnswers': {'brake_governor': 'pass'},
        'testLoadKg': 790,
        'finalResult': 'safe_to_operate',
        'submittedAt': Timestamp.fromDate(DateTime(2026, 9, 1)),
      }).toEntity();

      expect(legacy.templateId, 'CI-LIFT-01');
      expect(legacy.templateId, isNot(CertificateTemplate.id));
      expect(legacy.texts, isEmpty);
      expect(legacy.finalResult, CertificateResult.safeToOperate);
      expect(legacy.submittedAt, DateTime(2026, 9, 1));
    });
  });
}
