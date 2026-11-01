import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../machines/domain/entities/delivery.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../../machines/domain/entities/machine_status.dart';
import '../../../machines/domain/usecases/get_machine_pdf.dart';
import '../../../machines/presentation/widgets/machine_widgets.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/job_stepper.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/send_certificate_cubit.dart';
import '../widgets/delivery_certificate_tile.dart';
import 'delivery_email_page.dart';

/// The coordinator's last step on a job: the technical manager has approved
/// and signed its certificates, and the coordinator hands them over — looking
/// at each certificate's PDF, then previewing, editing and sending the email
/// that carries them ([DeliveryEmailPage]).
///
/// A job that can't be delivered (already handed over, say) shows the same
/// screen without the "Preview email" button, and with what was sent.
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

class _SendView extends StatelessWidget {
  final InspectionRequest request;

  const _SendView({required this.request});

  // Sending the email moves the job on, so the job reloads once the
  // coordinator is back from the email screen — to show what went out.
  Future<void> _openEmail(BuildContext context) async {
    final cubit = context.read<SendCertificateCubit>();
    final sent = await Navigator.of(context).push<bool>(
      MaterialPageRoute<bool>(builder: (_) => DeliveryEmailPage(request: request)),
    );
    if (sent == true) await cubit.start();
  }

  /// The machine's certificate PDF as the API renders it (the stored final
  /// file once approved), in the full-screen preview.
  Future<void> _viewPdf(BuildContext context, MachineSummary machine) {
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
    return Scaffold(
      backgroundColor: AppColours.background,
      body: Column(
        children: [
          _Header(request: request),
          Expanded(
            child: BlocBuilder<SendCertificateCubit, SendCertificateState>(
              builder: (context, state) {
                switch (state.status) {
                  case SendCertificateStatus.loading:
                    return const _Skeleton();
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

                final delivery = state.delivery;
                return RefreshIndicator(
                  color: AppColours.primaryColor,
                  onRefresh: context.read<SendCertificateCubit>().start,
                  child: ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                    children: [
                      if (delivery != null) ...[
                        _SentCard(delivery: delivery),
                        const SizedBox(height: 16),
                      ],
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 4),
                        child: Text(t.deliverCertificatesTitle, style: AppTextStyles.sectionTitle),
                      ),
                      const SizedBox(height: 12),
                      for (final machine in state.certificates) ...[
                        _CertificateCard(machine: machine, onOpen: () => _viewPdf(context, machine)),
                        const SizedBox(height: 12),
                      ],
                    ],
                  ),
                );
              },
            ),
          ),
          BlocBuilder<SendCertificateCubit, SendCertificateState>(
            builder: (context, state) {
              // Nothing to hand over (already delivered, or not ready): no bar
              // at all.
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
                  child: MPrimaryButton(label: t.previewEmailAction, onPressed: () => _openEmail(context)),
                ),
              );
            },
          ),
        ],
      ),
    );
  }
}

/// What went out to the client, as a receipt: a green check with when it was
/// sent, then who it went to (To, Cc) and its subject.
class _SentCard extends StatelessWidget {
  final DeliveryResult delivery;

  const _SentCard({required this.delivery});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final sentAt = delivery.sentAt?.toLocal();
    final locale = Localizations.localeOf(context).languageCode;

    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: const BoxDecoration(color: AppColours.successBackground, shape: BoxShape.circle),
                child: const Icon(Icons.check_rounded, size: 22, color: AppColours.successIcon),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(t.deliverySentTitle, style: AppTextStyles.cardTitle),
                    if (sentAt != null)
                      Text(
                        DateFormat.yMMMd(locale).add_Hm().format(sentAt),
                        style: AppTextStyles.caption.copyWith(height: 18 / 13),
                      ),
                  ],
                ),
              ),
            ],
          ),
          if (delivery.sentTo != null || delivery.cc.isNotEmpty || delivery.subject != null) ...[
            const Divider(height: 28, color: AppColours.borderSubtle),
            if (delivery.sentTo != null)
              _SentRow(icon: Icons.mail_outline_rounded, label: t.emailToLabel, value: delivery.sentTo!),
            if (delivery.cc.isNotEmpty) ...[
              const SizedBox(height: 14),
              _SentRow(icon: Icons.people_outline_rounded, label: t.emailCcLabel, value: delivery.cc.join(', ')),
            ],
            if (delivery.subject != null) ...[
              const SizedBox(height: 14),
              _SentRow(icon: Icons.subject_rounded, label: t.emailSubjectLabel, value: delivery.subject!),
            ],
          ],
          if (!delivery.emailSent) ...[
            const SizedBox(height: 16),
            MNotice(type: MNoticeType.info, message: t.certificateDeliveredNoEmailMessage),
          ],
        ],
      ),
    );
  }
}

/// One line of the sent receipt: an icon, a small label over the value.
class _SentRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;

  const _SentRow({required this.icon, required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 20, color: AppColours.inkMuted),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(label, style: AppTextStyles.caption.copyWith(fontSize: 12)),
              const SizedBox(height: 1),
              Text(
                value,
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// What the loading skeleton is drawn from; only its shape matters.
const _placeholder = MachineSummary(
  requestItemId: 'placeholder',
  name: 'Overhead crane — 10 t',
  quantity: 1,
  status: MachineStatus.approved,
  serialNumber: 'SN-000000',
  manufacturer: 'Manufacturer',
  certificateNumber: 'CERT-2026-0000',
  result: 'satisfactory',
  examinationDate: '2026-01-01',
  nextExaminationDate: '2026-07-01',
);

/// One approved certificate, in the machine card's language: the machine
/// (name, then what identifies it), a tinted panel with the certificate's
/// number, verdict and dates, and a footer with its status and "View PDF ›".
/// The whole card opens the final PDF.
class _CertificateCard extends StatelessWidget {
  final MachineSummary machine;
  final VoidCallback onOpen;

  const _CertificateCard({required this.machine, required this.onOpen});

  /// What identifies the machine on site.
  String get _facts => [machine.capacity, machine.manufacturer, machine.model, machine.serialNumber]
      .whereType<String>()
      .map((part) => part.trim())
      .where((part) => part.isNotEmpty)
      .join(' · ');

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final facts = _facts;
    final number = machine.certificateNumber;
    final result = machine.result;
    final examined = machine.examinationDate;
    final nextDue = machine.nextExaminationDate;

    return Container(
      decoration: BoxDecoration(color: Colors.white, borderRadius: BorderRadius.circular(24)),
      clipBehavior: Clip.antiAlias,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onOpen,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(20, 18, 20, 14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      child: Text(
                        machine.name,
                        style: AppTextStyles.cardTitle,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                    if (machine.quantity > 1) ...[
                      const SizedBox(width: 12),
                      Text(t.machineQuantityLabel(machine.quantity), style: AppTextStyles.caption),
                    ],
                  ],
                ),
                if (facts.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(
                    facts,
                    style: AppTextStyles.caption.copyWith(height: 18 / 13),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ],
                if (number != null || result != null || examined != null || nextDue != null) ...[
                  const SizedBox(height: 14),
                  Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(color: AppColours.surfaceMuted, borderRadius: BorderRadius.circular(16)),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        if (number != null || result != null)
                          Row(
                            children: [
                              Expanded(
                                child: Text(
                                  number == null ? '' : t.machineCertificateNumber(number),
                                  style: AppTextStyles.caption.copyWith(
                                    color: AppColours.successText,
                                    fontWeight: FontWeight.w700,
                                  ),
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                              if (result != null) ...[const SizedBox(width: 8), CertificateResultChip(result: result)],
                            ],
                          ),
                        if ((number != null || result != null) && (examined != null || nextDue != null))
                          const SizedBox(height: 12),
                        if (examined != null || nextDue != null)
                          Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              if (examined != null)
                                Expanded(child: _DateColumn(label: t.examinationDateLabel, value: examined)),
                              if (examined != null && nextDue != null) const SizedBox(width: 12),
                              if (nextDue != null)
                                Expanded(child: _DateColumn(label: t.nextExaminationDateLabel, value: nextDue)),
                            ],
                          ),
                      ],
                    ),
                  ),
                ],
                const SizedBox(height: 14),
                Row(
                  children: [
                    MachineStatusChip(status: machine.status),
                    const Spacer(),
                    Text(t.viewPdfAction, style: AppTextStyles.link.copyWith(fontSize: 14)),
                    const SizedBox(width: 2),
                    const Icon(Icons.chevron_right_rounded, size: 20, color: AppColours.primaryColor),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// A small label over a date, for the certificate card's panel.
class _DateColumn extends StatelessWidget {
  final String label;
  final String value;

  const _DateColumn({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: AppTextStyles.caption.copyWith(fontSize: 12)),
        const SizedBox(height: 2),
        Text(
          formatCertificateDate(context, value),
          style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
        ),
      ],
    );
  }
}

/// The loading state: certificate cards drawn from [_placeholder],
/// shimmering.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Skeletonizer(
      child: ListView(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 4),
            child: Text(t.deliverCertificatesTitle, style: AppTextStyles.sectionTitle),
          ),
          const SizedBox(height: 12),
          for (var i = 0; i < 2; i++) ...[
            _CertificateCard(machine: _placeholder, onOpen: () {}),
            const SizedBox(height: 12),
          ],
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
