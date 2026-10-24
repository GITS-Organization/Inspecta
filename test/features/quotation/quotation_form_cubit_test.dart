import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/quotation/domain/entities/pricing_unit.dart';
import 'package:inspecta/features/quotation/domain/entities/quotation_failure.dart';
import 'package:inspecta/features/quotation/domain/entities/reply_type.dart';
import 'package:inspecta/features/quotation/domain/usecases/save_draft.dart';
import 'package:inspecta/features/quotation/domain/usecases/send_quotation.dart';
import 'package:inspecta/features/quotation/presentation/bloc/quotation_form_cubit.dart';
import 'package:inspecta/features/requests/domain/entities/requests_failure.dart';

import '../requests/fake_requests_repository.dart';
import 'fake_quotation_repository.dart';

void main() {
  late FakeRequestsRepository requestsRepo;
  late FakeQuotationRepository quotationRepo;

  // sampleRequest()'s default item is quantity 2, matching acceptance
  // criterion 1's "2-unit item".
  QuotationFormCubit buildCubit() {
    requestsRepo = FakeRequestsRepository();
    quotationRepo = FakeQuotationRepository();
    return QuotationFormCubit(
      request: sampleRequest(),
      saveDraft: SaveDraft(quotationRepo),
      sendQuotation: SendQuotation(quotationRepo, requestsRepo),
    );
  }

  group('QuotationFormCubit', () {
    test('an offer of EGP 4,500 for a 2-unit item totals EGP 9,000 (acceptance criterion 1)', () {
      final cubit = buildCubit();

      cubit.selectReplyType(ReplyType.offer);
      cubit.changeLinePrice('item-1', '4500');
      cubit.changeScopeOfWork(['Load test']);

      expect(cubit.state.unitPriceOf('item-1'), 450000);
      expect(cubit.totalPiastres, 900000);
      expect(cubit.state.isValid, isTrue);
    });

test('an offer needs at least one scope point before it can be sent', () async {
      final cubit = buildCubit();
      cubit.selectReplyType(ReplyType.offer);
      cubit.changeLinePrice('item-1', '4500');
      cubit.changeScopeOfWork(['   ']);

      await cubit.send();

      expect(cubit.state.showScopeError, isTrue);
      expect(cubit.state.showPriceError, isFalse);
      expect(quotationRepo.sendCalls, isEmpty);

      cubit.changeScopeOfWork(['Load test']);
      expect(cubit.state.showScopeError, isFalse);
      await cubit.send();
      expect(quotationRepo.sendCalls.single.scopeOfWork, ['Load test']);
    });

    test('an offer needs a price before it can be sent', () async {
      final cubit = buildCubit();
      cubit.selectReplyType(ReplyType.offer);

      await cubit.send();

      expect(cubit.state.showPriceError, isTrue);
      expect(quotationRepo.sendCalls, isEmpty);
    });

    test(
      'reject cannot be sent without a reason (acceptance criterion 2)',
      () async {
        final cubit = buildCubit();
        cubit.selectReplyType(ReplyType.reject);

        await cubit.send();
        expect(cubit.state.showReasonError, isTrue);
        expect(requestsRepo.rejections, isEmpty);

        cubit.changeMessage('Outside our certified scope.');
        await cubit.send();

        expect(
          requestsRepo.rejections.single.$2,
          'Outside our certified scope.',
        );
        expect(cubit.state.lastActionSuccess, isTrue);
      },
    );

    test('sending an offer creates a quotation and moves the request to quote_sent', () async {
      final cubit = buildCubit();
      cubit.selectReplyType(ReplyType.offer);
      cubit.changeLinePrice('item-1', '4500');
      cubit.changeScopeOfWork(['  Load test of both cranes  ', '   ']);

      await cubit.send();

      expect(quotationRepo.sendCalls.single.type, ReplyType.offer);
      expect(quotationRepo.sendCalls.single.items.single.pricingUnit, PricingUnit.perVisit);
      expect(quotationRepo.sendCalls.single.scopeOfWork, ['Load test of both cranes']);
      expect(requestsRepo.statusUpdates.single.$1, cubit.request.id);
      expect(cubit.state.lastActionSuccess, isTrue);
      expect(cubit.state.sentVersion, 1);
    });

    test('accept needs no price or reason to send', () async {
      final cubit = buildCubit();
      cubit.selectReplyType(ReplyType.accept);

      await cubit.send();

      expect(quotationRepo.sendCalls.single.type, ReplyType.accept);
      expect(cubit.state.lastActionSuccess, isTrue);
    });

    test('save draft is a no-op for reject', () async {
      final cubit = buildCubit();
      cubit.selectReplyType(ReplyType.reject);

      await cubit.saveDraft();

      expect(quotationRepo.saveDraftCalls, isEmpty);
    });

    test('save draft calls the repository for accept/offer', () async {
      final cubit = buildCubit();
      cubit.selectReplyType(ReplyType.offer);
      cubit.changeLinePrice('item-1', '100');

      await cubit.saveDraft();

      expect(quotationRepo.saveDraftCalls, hasLength(1));
      expect(cubit.state.lastAction, QuotationAction.saveDraft);
      expect(cubit.state.lastActionSuccess, isTrue);
    });

    test(
      'surfaces a repository failure from the underlying repository',
      () async {
        final cubit = buildCubit();
        quotationRepo.sendFailure = const QuotationFailure(
          QuotationFailureCode.network,
        );
        cubit.selectReplyType(ReplyType.accept);

        await cubit.send();

        expect(cubit.state.lastActionSuccess, isFalse);
        expect(cubit.state.lastActionFailure, QuotationFailureCode.network);
      },
    );

    test(
      'surfaces a RequestsFailure from rejecting (a different repository)',
      () async {
        final cubit = buildCubit();
        requestsRepo.rejectFailure = const RequestsFailure(
          RequestsFailureCode.permissionDenied,
        );
        cubit.selectReplyType(ReplyType.reject);
        cubit.changeMessage('reason');

        await cubit.send();

        expect(cubit.state.lastActionSuccess, isFalse);
        expect(
          cubit.state.lastActionFailure,
          QuotationFailureCode.permissionDenied,
        );
      },
    );
  });
}
