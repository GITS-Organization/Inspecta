import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/review_detail_cubit.dart';
import 'return_note_dialog.dart';

/// "Return" (asks for a reason) and "Approve & send to client" (asks to
/// confirm) — approving stays disabled until the certificate is signed,
/// named, licensed and addressed.
class ReviewFooter extends StatelessWidget {
  const ReviewFooter({super.key});

  Future<void> _confirmApprove(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<ReviewDetailCubit>();
    final state = cubit.state;
    final confirmed = await showAdaptiveDialog<bool>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(t.confirmApproveTitle),
        content: Text(t.confirmApproveMessage(state.reviewerName.trim())),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: Text(t.cancel)),
          TextButton(onPressed: () => Navigator.of(context).pop(true), child: Text(t.approveConfirmAction)),
        ],
      ),
    );
    if (confirmed == true) await cubit.approve();
  }

  Future<void> _return(BuildContext context) async {
    final cubit = context.read<ReviewDetailCubit>();
    final note = await ReturnNoteDialog.show(context);
    if (note != null) await cubit.sendBack(note);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: AppColours.borderSubtle)),
      ),
      child: SafeArea(
        top: false,
        minimum: const EdgeInsets.only(bottom: 6),
        child: BlocBuilder<ReviewDetailCubit, ReviewDetailState>(
          builder: (context, state) {
            return Row(
              children: [
                Expanded(
                  flex: 2,
                  child: SizedBox(
                    height: 54,
                    child: OutlinedButton(
                      onPressed: state.certificate == null || state.isBusy ? null : () => _return(context),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: AppColours.dangerText,
                        side: const BorderSide(color: AppColours.dangerBorder),
                        textStyle: AppTextStyles.buttonLabel,
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                      ),
                      child: state.isReturning
                          ? const SizedBox.square(dimension: 22, child: CircularProgressIndicator(strokeWidth: 2.5))
                          : Text(t.returnAction),
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  flex: 5,
                  child: MPrimaryButton(
                    label: t.approveAndSendAction,
                    loading: state.isApproving,
                    onPressed: state.canApprove && !state.isBusy ? () => _confirmApprove(context) : null,
                  ),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}
