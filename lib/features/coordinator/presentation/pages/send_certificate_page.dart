import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/shared/m_text_field.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/presentation/pdf/certificate_pdf_preview.dart';
import '../../../certificate/presentation/widgets/certificate_labels.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../technical_manager/presentation/widgets/review_summary_card.dart';
import '../bloc/send_certificate_cubit.dart';

/// The coordinator's last step on a job: the technical manager has approved
/// and signed the certificate, and the coordinator sends it to the client —
/// downloading the signed PDF, picking the address, and marking it as sent.
class SendCertificatePage extends StatelessWidget {
  final InspectionRequest request;

  const SendCertificatePage({super.key, required this.request});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<SendCertificateCubit>(param1: request)..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.light,
        child: _SendView(request: request),
      ),
    );
  }
}

class _SendView extends StatefulWidget {
  final InspectionRequest request;

  const _SendView({required this.request});

  @override
  State<_SendView> createState() => _SendViewState();
}

class _SendViewState extends State<_SendView> {
  final _email = TextEditingController();

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _confirmSend() async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<SendCertificateCubit>();
    final confirmed = await showAdaptiveDialog<bool>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(t.confirmSendTitle),
        content: Text(t.confirmSendMessage(cubit.state.email.trim())),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: Text(t.cancel)),
          TextButton(onPressed: () => Navigator.of(context).pop(true), child: Text(t.sendToClientAction)),
        ],
      ),
    );
    if (confirmed == true) await cubit.send();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final request = widget.request;
    return MultiBlocListener(
      listeners: [
        BlocListener<SendCertificateCubit, SendCertificateState>(
          listenWhen: (previous, current) => previous.suggestedEmail != current.suggestedEmail,
          listener: (context, state) {
            if (_email.text.isEmpty && state.suggestedEmail != null) _email.text = state.suggestedEmail!;
          },
        ),
        BlocListener<SendCertificateCubit, SendCertificateState>(
          listenWhen: (previous, current) => previous.actionSeq != current.actionSeq,
          listener: (context, state) {
            if (state.lastActionSuccess) {
              MToast.showSuccess(message: t.certificateSentMessage);
              Navigator.of(context).pop();
            } else if (state.lastActionFailure != null) {
              MToast.showError(message: state.lastActionFailure!.message(t));
            }
          },
        ),
      ],
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            _Header(request: request),
            Expanded(
              child: BlocBuilder<SendCertificateCubit, SendCertificateState>(
                builder: (context, state) {
                  switch (state.status) {
                    case SendCertificateStatus.loading:
                      return Loading.loader(context);
                    case SendCertificateStatus.error:
                    case SendCertificateStatus.notFound:
                      return Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: MNotice(type: MNoticeType.error, message: t.certificateNotFound),
                        ),
                      );
                    case SendCertificateStatus.ready:
                      break;
                  }
                  final certificate = state.certificate;
                  if (certificate == null) return const SizedBox.shrink();
                  return ListView(
                    padding: const EdgeInsets.fromLTRB(20, 18, 20, 24),
                    children: [
                      ReviewSummaryCard(
                        request: request,
                        certificate: certificate,
                        onViewPdf: () => _viewPdf(certificate),
                      ),
                      const SizedBox(height: 14),
                      _QrCodeCard(code: certificate.verificationCode),
                      const SizedBox(height: 12),
                      SizedBox(
                        height: 52,
                        width: double.infinity,
                        child: OutlinedButton.icon(
                          onPressed: () => _viewPdf(certificate),
                          icon: const Icon(Icons.picture_as_pdf_outlined),
                          label: Text(t.previewFinalPdfAction),
                          style: OutlinedButton.styleFrom(
                            foregroundColor: AppColours.primaryDark,
                            side: const BorderSide(color: AppColours.primaryColor, width: 1.5),
                            textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 15),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                          ),
                        ),
                      ),
                      const SizedBox(height: 18),
                      Text(t.sendSignedPdfToLabel, style: AppTextStyles.cardTitle.copyWith(fontSize: 16)),
                      const SizedBox(height: 8),
                      MTextField(
                        controller: _email,
                        onChanged: context.read<SendCertificateCubit>().setEmail,
                        enabled: !state.isSending,
                        prefixIcon: Icons.mail_outline_rounded,
                        keyboardType: TextInputType.emailAddress,
                        hintText: t.emailHint,
                      ),
                      const SizedBox(height: 12),
                      Text(t.sendCertificateHint, style: AppTextStyles.caption),
                    ],
                  );
                },
              ),
            ),
            Container(
              padding: const EdgeInsets.fromLTRB(20, 14, 20, 22),
              decoration: const BoxDecoration(
                color: Colors.white,
                border: Border(top: BorderSide(color: AppColours.border)),
              ),
              child: BlocBuilder<SendCertificateCubit, SendCertificateState>(
                builder: (context, state) => MPrimaryButton(
                  label: t.sendToClientAction,
                  loading: state.isSending,
                  onPressed: state.canSend ? _confirmSend : null,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _viewPdf(Certificate certificate) {
    return previewCertificatePdf(context, request: widget.request, certificate: certificate);
  }
}

/// The code the technical manager's approval issued — what the QR on the PDF
/// (and on the dashboard) carries — or a note when the certificate predates
/// QR codes.
class _QrCodeCard extends StatelessWidget {
  final String? code;

  const _QrCodeCard({required this.code});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final issued = code != null;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: issued ? AppColours.successBackground : AppColours.chipAmberBackground,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        children: [
          Icon(
            Icons.qr_code_2_rounded,
            size: 32,
            color: issued ? AppColours.successIcon : AppColours.chipAmberText,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(t.verificationCodeLabel, style: AppTextStyles.caption),
                const SizedBox(height: 2),
                Text(
                  code ?? t.noVerificationCodeHint,
                  style: issued
                      ? const TextStyle(
                          fontFamily: 'monospace',
                          fontSize: 16,
                          fontWeight: FontWeight.w700,
                          color: AppColours.successText,
                        )
                      : AppTextStyles.subtitle.copyWith(fontSize: 13, color: AppColours.chipAmberText),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Header extends StatelessWidget {
  final InspectionRequest request;

  const _Header({required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 16, 20, 20),
      decoration: const BoxDecoration(
        color: AppColours.primaryColor,
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
      ),
      child: Row(
        children: [
          IconButton(
            onPressed: () => Navigator.of(context).maybePop(),
            tooltip: t.back,
            style: IconButton.styleFrom(
              fixedSize: const Size.square(44),
              backgroundColor: Colors.white.withValues(alpha: 0.16),
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
            ),
            icon: const Icon(Icons.chevron_left_rounded, size: 26),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(request.clientName, style: AppTextStyles.caption.copyWith(color: AppColours.onPrimaryMuted)),
                Text(
                  t.sendCertificateTitle,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.cardTitle.copyWith(color: Colors.white, fontSize: 21),
                ),
              ],
            ),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.16),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Text(
              certNumberFor(request),
              style: const TextStyle(
                fontFamily: 'monospace',
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: Colors.white,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
