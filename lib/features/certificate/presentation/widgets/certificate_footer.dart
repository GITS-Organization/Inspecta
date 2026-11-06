import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/certificate_cubit.dart';
import '../pdf/certificate_pdf_preview.dart';
import 'certificate_labels.dart';
import 'certificate_missing_panel.dart';

/// "Preview" (the draft's PDF, full screen) and "Submit to technical
/// manager" — enabled only once every section of the report is filled in.
class CertificateFooter extends StatelessWidget {
  const CertificateFooter({super.key});

  /// The machines are sent together from the job's list ("Submit all"), so a
  /// machine's screen only keeps what was typed and goes back.
  Future<void> _saveAndBack(BuildContext context) async {
    await context.read<CertificateCubit>().persist();
    if (context.mounted) Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: const EdgeInsets.fromLTRB(20, 14, 20, 22),
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: AppColours.border)),
      ),
      child: BlocConsumer<CertificateCubit, CertificateState>(
        listenWhen: (previous, current) => current.actionSeq != previous.actionSeq,
        listener: (context, state) {
          if (state.lastActionSuccess) {
            MToast.showSuccess(message: t.certificateSubmittedMessage);
            Navigator.of(context).pop();
          } else if (state.lastActionFailure != null) {
            MToast.showError(message: state.lastActionFailure!.message(t));
          }
        },
        builder: (context, state) {
          final blockers = state.machineMode && !state.apiReady ? state.missing : const <String>[];
          return Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (blockers.isNotEmpty) ...[
                CertificateMissingPanel(items: blockers),
                const SizedBox(height: 10),
              ],
              Row(
                children: [
                  Expanded(
                    child: SizedBox(
                      height: 52,
                      child: OutlinedButton(
                        onPressed: state.certificate == null
                            ? null
                            : () => previewCertificatePdf(
                                context,
                                request: context.read<CertificateCubit>().request,
                                certificate: state.certificate!,
                              ),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: AppColours.inkBody,
                          backgroundColor: Colors.white,
                          side: const BorderSide(color: AppColours.border, width: 1.5),
                          textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14, color: AppColours.inkBody),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                        ),
                        child: Text(t.previewAction),
                      ),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    flex: 2,
                    child: SizedBox(
                      height: 52,
                      child: MPrimaryButton(
                        label: t.saveAndBackAction,
                        onPressed: state.certificate == null ? null : () => _saveAndBack(context),
                      ),
                    ),
                  ),
                ],
              ),
            ],
          );
        },
      ),
    );
  }
}
