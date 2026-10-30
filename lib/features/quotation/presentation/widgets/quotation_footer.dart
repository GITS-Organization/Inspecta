import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/framework/mtoast.dart';
import '../../../../core/utils/currency.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/reply_type.dart';
import '../bloc/quotation_form_cubit.dart';
import 'quotation_labels.dart';
import 'quote_total_bar.dart';

/// Wires [QuoteTotalBar] to the [QuotationFormCubit]: the running total
/// for whichever reply type is selected, and Save draft / Send —
/// including the actual save/send, not just validation.
///
/// A successful send pops back to the inbox (the request has moved out
/// of New); a successful save draft or reject stays on the wrapped page
/// so its caller decides what to do (see [RequestDetailPage]).
class QuotationFooter extends StatelessWidget {
  final String unitsLabel;
  final VoidCallback onSent;

  const QuotationFooter({
    super.key,
    required this.unitsLabel,
    required this.onSent,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocListener<QuotationFormCubit, QuotationFormState>(
      listenWhen: (previous, current) => current.actionSeq != previous.actionSeq,
      listener: (context, state) {
        if (!state.lastActionSuccess) {
          MToast.showError(message: state.lastActionFailure!.message(t));
          return;
        }
        switch (state.lastAction!) {
          case QuotationAction.saveDraft:
            MToast.showSuccess(message: t.draftSaved);
          case QuotationAction.send:
            final cubit = context.read<QuotationFormCubit>();
            MToast.showSuccess(
              message: cubit.state.replyType == ReplyType.reject
                  ? t.requestRejected
                  : t.quotationSent(state.sentVersion ?? 1),
            );
            onSent();
        }
      },
      child: BlocBuilder<QuotationFormCubit, QuotationFormState>(
        builder: (context, state) {
          final cubit = context.read<QuotationFormCubit>();

          final String totalText;
          if (state.isPriced && !state.priceInvalid) {
            totalText = Currency.formatEgp(cubit.totalPiastres!);
          } else {
            totalText = '—';
          }

          final canSaveDraft =
              !state.isSubmitting &&
              state.replyType != null &&
              state.replyType != ReplyType.reject;

          return QuoteTotalBar(
            unitsLabel: unitsLabel,
            totalText: totalText,
            loading: state.isSubmitting,
            onSaveDraft: canSaveDraft ? cubit.saveDraft : null,
            onSend: state.isSubmitting
                ? null
                : () {
                    if (state.replyType == null) {
                      MToast.showError(message: t.chooseReplyTypeFirst);
                      return;
                    }
                    cubit.send();
                  },
          );
        },
      ),
    );
  }
}
