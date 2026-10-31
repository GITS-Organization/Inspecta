import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_text_field.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/presentation/widgets/certificate_labels.dart';
import '../../../machines/domain/entities/delivery.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../../machines/domain/usecases/get_machine_pdf.dart';
import '../../../machines/presentation/widgets/machine_widgets.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/job_stepper.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/send_certificate_cubit.dart';

/// The coordinator's last step on a job: the technical manager has approved
/// and signed its certificates, and the coordinator hands them over — looking
/// at each certificate's PDF and the email that goes with them, then marking
/// the job as delivered.
///
/// A job that can't be delivered (already handed over, say) shows the same
/// screen without the Deliver button.
class SendCertificatePage extends StatelessWidget {
  final InspectionRequest request;

  const SendCertificatePage({super.key, required this.request});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<SendCertificateCubit>(param1: request)..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
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
  final _message = TextEditingController();

  @override
  void dispose() {
    _email.dispose();
    _message.dispose();
    super.dispose();
  }

  Future<void> _confirmSend() async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<SendCertificateCubit>();
    final to = cubit.state.email.trim();
    final confirmed = await showAdaptiveDialog<bool>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(t.confirmSendTitle),
        content: Text(t.confirmSendMessage(to.isEmpty ? widget.request.clientName : to)),
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
            if (_email.text.isEmpty && state.suggestedEmail != null) {
              _email.text = state.suggestedEmail!;
            }
          },
        ),
        BlocListener<SendCertificateCubit, SendCertificateState>(
          listenWhen: (previous, current) => previous.actionSeq != current.actionSeq,
          listener: (context, state) {
            if (state.lastActionSuccess) {
              // The API hands the job over but doesn't send the email yet; say
              // so rather than claiming the client has it.
              MToast.showSuccess(
                message: state.lastResult?.emailSent == true
                    ? t.certificateSentMessage
                    : t.certificateDeliveredNoEmailMessage,
              );
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
                      return Center(
                        child: Padding(
                          padding: const EdgeInsets.all(24),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              MNotice(type: MNoticeType.error, message: state.loadFailure!.message(t)),
                              const SizedBox(height: 16),
                              OutlinedButton(onPressed: context.read<SendCertificateCubit>().retry, child: Text(t.retry)),
                            ],
                          ),
                        ),
                      );
                    case SendCertificateStatus.ready:
                      break;
                  }
                  return ListView(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                    children: [
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 4),
                        child: Text(t.deliverCertificatesTitle, style: AppTextStyles.sectionTitle),
                      ),
                      const SizedBox(height: 12),
                      for (final machine in state.certificates) ...[
                        _CertificateCard(request: request, machine: machine),
                        const SizedBox(height: 12),
                      ],
                      if (state.canDeliver) ...[
                        const SizedBox(height: 8),
                        _EmailCard(
                          preview: state.preview,
                          email: _email,
                          message: _message,
                          enabled: !state.isSending,
                        ),
                      ],
                    ],
                  );
                },
              ),
            ),
            BlocBuilder<SendCertificateCubit, SendCertificateState>(
              builder: (context, state) {
                // Nothing to hand over (already delivered, or not ready): no
                // bar at all.
                if (state.status != SendCertificateStatus.ready || !state.canDeliver) return const SizedBox.shrink();
                return Container(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
                  decoration: const BoxDecoration(
                    color: Colors.white,
                    border: Border(top: BorderSide(color: AppColours.borderSubtle)),
                  ),
                  child: SafeArea(
                    top: false,
                    minimum: const EdgeInsets.only(bottom: 6),
                    child: SizedBox(
                      height: 54,
                      width: double.infinity,
                      child: FilledButton(
                        onPressed: state.canSend ? _confirmSend : null,
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColours.primaryColor,
                          disabledBackgroundColor: state.isSending
                              ? AppColours.primaryColor
                              : AppColours.primaryColor.withValues(alpha: 0.4),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                        ),
                        child: state.isSending
                            ? const SizedBox.square(
                                dimension: 20,
                                child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white),
                              )
                            : Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  const Icon(Icons.send_rounded, size: 18, color: Colors.white),
                                  const SizedBox(width: 8),
                                  Text(t.sendToClientAction, style: AppTextStyles.buttonLabel),
                                ],
                              ),
                      ),
                    ),
                  ),
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}

/// One approved certificate: its status and number, the machine it covers,
/// and a "Preview" link to the final PDF.
class _CertificateCard extends StatelessWidget {
  final InspectionRequest request;
  final MachineSummary machine;

  const _CertificateCard({required this.request, required this.machine});

  Future<void> _preview(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final number = machine.certificateNumber ?? certNumberFor(request);
    return PdfPreviewPage.open(
      context,
      title: t.certificateTitle,
      subtitle: number,
      fileName: '$number.pdf',
      build: () => getIt<GetMachinePdf>()(request.id, machine.requestItemId),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              MachineStatusChip(status: machine.status),
              const Spacer(),
              Text(t.machineQuantityLabel(machine.quantity), style: AppTextStyles.caption),
            ],
          ),
          const SizedBox(height: 12),
          Text(machine.name, style: AppTextStyles.cardTitle),
          if (machine.serialNumber != null) ...[
            const SizedBox(height: 2),
            Text(machine.serialNumber!, style: AppTextStyles.caption.copyWith(height: 18 / 13)),
          ],
          if (machine.certificateNumber != null) ...[
            const SizedBox(height: 10),
            Text(
              t.machineCertificateNumber(machine.certificateNumber!),
              style: AppTextStyles.caption.copyWith(color: AppColours.successText, fontWeight: FontWeight.w600),
            ),
          ],
          const SizedBox(height: 12),
          Align(
            alignment: AlignmentDirectional.centerEnd,
            child: InkWell(
              onTap: () => _preview(context),
              borderRadius: BorderRadius.circular(8),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 6),
                child: Text(t.previewAction, style: AppTextStyles.link.copyWith(fontSize: 14)),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The email that goes with the certificates: who it's for (editable), what
/// it says and which files it carries, and an optional message of the
/// coordinator's own.
class _EmailCard extends StatelessWidget {
  final DeliveryPreview? preview;
  final TextEditingController email;
  final TextEditingController message;
  final bool enabled;

  const _EmailCard({required this.preview, required this.email, required this.message, required this.enabled});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<SendCertificateCubit>();
    final preview = this.preview;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(t.sendSignedPdfToLabel, style: AppTextStyles.sectionTitle.copyWith(fontSize: 14)),
          const SizedBox(height: 12),
          MTextField(
            controller: email,
            onChanged: cubit.setEmail,
            enabled: enabled,
            prefixIcon: Icons.mail_outline_rounded,
            keyboardType: TextInputType.emailAddress,
            hintText: t.emailHint,
          ),
          if (preview != null && (preview.subject.isNotEmpty || preview.text.isNotEmpty)) ...[
            const SizedBox(height: 16),
            Text(t.emailPreviewTitle, style: AppTextStyles.badge),
            const SizedBox(height: 8),
            if (preview.subject.isNotEmpty) Text(preview.subject, style: AppTextStyles.cardTitle.copyWith(fontSize: 15)),
            if (preview.text.isNotEmpty) ...[
              const SizedBox(height: 6),
              Text(preview.text, style: AppTextStyles.body),
            ],
            if (preview.attachments.isNotEmpty) ...[
              const SizedBox(height: 10),
              for (final name in preview.attachments)
                Padding(
                  padding: const EdgeInsets.only(top: 4),
                  child: Row(
                    children: [
                      const Icon(Icons.attach_file_rounded, size: 15, color: AppColours.inkMuted),
                      const SizedBox(width: 4),
                      Expanded(child: Text(name, style: AppTextStyles.caption, overflow: TextOverflow.ellipsis)),
                    ],
                  ),
                ),
            ],
          ],
          const SizedBox(height: 16),
          TextField(
            controller: message,
            onChanged: cubit.setMessage,
            enabled: enabled,
            minLines: 2,
            maxLines: 4,
            textInputAction: TextInputAction.newline,
            decoration: InputDecoration(
              hintText: t.deliverMessageHint,
              border: OutlineInputBorder(borderRadius: BorderRadius.circular(14)),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(14),
                borderSide: const BorderSide(color: AppColours.primaryColor, width: 2),
              ),
            ),
          ),
          const SizedBox(height: 12),
          Text(t.deliverNoEmailHint, style: AppTextStyles.caption),
        ],
      ),
    );
  }
}

/// The banner: back button, certificate number and client, the title, and
/// the job's progress row — which, with the certificate approved, shows
/// everything done up to Certify and only "Sent" left.
class _Header extends StatelessWidget {
  final InspectionRequest request;

  const _Header({required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AppBanner(
      topInset: MediaQuery.paddingOf(context).top,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              const MBackButton(),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      '${certNumberFor(request)} · ${request.clientName}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.caption.copyWith(
                        fontWeight: FontWeight.w600,
                        letterSpacing: 0.24,
                        height: 16 / 12,
                        fontSize: 12,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      t.sendCertificateTitle,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.sectionTitle.copyWith(
                        fontSize: 22,
                        height: 28 / 22,
                        letterSpacing: -0.22,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 18),
          JobStepper(status: request.status),
        ],
      ),
    );
  }
}
