import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/presentation/widgets/certificate_labels.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../../machines/domain/entities/machine_status.dart';
import '../../../machines/domain/usecases/get_machine_pdf.dart';
import '../../../machines/presentation/widgets/machine_widgets.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_source.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/review_summary_cubit.dart';
import '../widgets/return_note_dialog.dart';

/// What the technical manager opens from a Review-tab card: the job's
/// `to-review` row, then one expanded card per machine with a "Preview"
/// button for its certificate PDF, and the two decisions on the whole job —
/// approve every certificate or reject them all with a note.
class ReviewSummaryPage extends StatelessWidget {
  final InspectionRequest request;

  const ReviewSummaryPage({super.key, required this.request});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<ReviewSummaryCubit>(param1: request)..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: const _SummaryView(),
      ),
    );
  }
}

class _SummaryView extends StatelessWidget {
  const _SummaryView();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocListener<ReviewSummaryCubit, ReviewSummaryState>(
      listenWhen: (previous, current) =>
          previous.actionSeq != current.actionSeq,
      listener: (context, state) {
        final action = state.lastAction;
        if (action == null) return;
        if (action.succeeded) {
          MToast.showSuccess(
            message: action.approved
                ? t.certificateApprovedMessage
                : t.certificateReturnedMessage,
          );
          Navigator.of(context).pop();
        } else {
          // A refused approval says which machines aren't ready; otherwise
          // the generic message for what went wrong.
          MToast.showError(
            message: action.problems.isEmpty
                ? action.failure!.message(t)
                : action.problems.join('\n'),
          );
        }
      },
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            BlocSelector<
              ReviewSummaryCubit,
              ReviewSummaryState,
              InspectionRequest
            >(
              selector: (state) => state.request,
              builder: (context, request) => MachinesHeader(
                eyebrow: request.clientName.isEmpty
                    ? request.id
                    : request.clientName,
                title: t.reviewJobSummaryTitle,
              ),
            ),
            const Expanded(child: _Body()),
            const _DecisionBar(),
          ],
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<ReviewSummaryCubit, ReviewSummaryState>(
      builder: (context, state) {
        switch (state.status) {
          case ReviewSummaryStatus.loading:
            return const _Skeleton();
          case ReviewSummaryStatus.error:
            return Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    MNotice(
                      type: MNoticeType.error,
                      message: state.loadFailure!.message(t),
                    ),
                    const SizedBox(height: 16),
                    OutlinedButton(
                      onPressed: context.read<ReviewSummaryCubit>().retry,
                      child: Text(t.retry),
                    ),
                  ],
                ),
              ),
            );
          case ReviewSummaryStatus.ready:
            break;
        }
        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
          children: [
            _JobCard(request: state.request),
            if (state.reviewHistory.isNotEmpty) ...[
              const SizedBox(height: 12),
              _HistoryCard(history: state.reviewHistory),
            ],
            const SizedBox(height: 20),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Text(
                t.equipmentToReviewTitle,
                style: AppTextStyles.sectionTitle,
              ),
            ),
            const SizedBox(height: 12),
            if (state.machines.isEmpty)
              SizedBox(height: 240, child: EmptyState(title: t.machinesEmpty))
            else
              for (final machine in state.machines) ...[
                _MachineSummaryCard(request: state.request, machine: machine),
                const SizedBox(height: 12),
              ],
            // When approving isn't possible, these are why — shown in place of
            // the Approve button.
            if (state.canReview &&
                !state.canApprove &&
                state.reviewBlockers.isNotEmpty) ...[
              const SizedBox(height: 4),
              _BlockersCard(blockers: state.reviewBlockers),
            ],
          ],
        );
      },
    );
  }
}

/// The loading state: the job card and a machine card drawn from
/// placeholders, shimmering — as on the machines screen.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  static const _machine = MachineSummary(
    requestItemId: 'placeholder',
    name: 'Overhead crane — 10 t',
    quantity: 1,
    status: MachineStatus.submitted,
    serialNumber: 'SN-000000',
    checklistTotal: 20,
    checklistAnswered: 20,
  );

  @override
  Widget build(BuildContext context) {
    final job = InspectionRequest(
      id: 'placeholder',
      source: RequestSource.email,
      clientName: 'Client name',
      receivedAt: DateTime(2000),
      location: 'City, Country',
      status: JobStatus.certificateSubmitted,
      items: const [],
      certificates: const CertificateCounts(total: 2, submitted: 2),
    );
    return Skeletonizer(
      child: ListView(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
        children: [
          _JobCard(request: job),
          const SizedBox(height: 20),
          for (var i = 0; i < 2; i++) ...[
            _MachineSummaryCard(request: job, machine: _machine),
            const SizedBox(height: 12),
          ],
        ],
      ),
    );
  }
}

class _JobCard extends StatelessWidget {
  final InspectionRequest request;

  const _JobCard({required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final scheduledAt = request.scheduledAt?.toLocal();
    final counts = request.certificates;
    final value = counts == null || counts.total == 0
        ? 0.0
        : (counts.submitted / counts.total).clamp(0.0, 1.0);

    // Laid out like the summary on the machines screen, so the two read the
    // same.
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (request.clientName.isNotEmpty)
            Text(request.clientName, style: AppTextStyles.cardTitle),
          if (request.location.isNotEmpty) ...[
            const SizedBox(height: 4),
            Row(
              children: [
                const Icon(
                  Icons.location_on_outlined,
                  size: 15,
                  color: AppColours.inkMuted,
                ),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    request.location,
                    style: AppTextStyles.caption,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
          ],
          if (scheduledAt != null) ...[
            const SizedBox(height: 4),
            Row(
              children: [
                const Icon(
                  Icons.event_outlined,
                  size: 15,
                  color: AppColours.inkMuted,
                ),
                const SizedBox(width: 4),
                Text(
                  DateFormat('EEE, d MMM · HH:mm', locale).format(scheduledAt),
                  style: AppTextStyles.caption,
                ),
              ],
            ),
          ],
          if (counts != null) ...[
            const SizedBox(height: 16),
            ClipRRect(
              borderRadius: BorderRadius.circular(999),
              child: LinearProgressIndicator(
                value: value,
                minHeight: 8,
                color: AppColours.successIcon,
                backgroundColor: AppColours.primaryTint,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              t.machinesProgress(counts.submitted, counts.total),
              style: AppTextStyles.caption,
            ),
          ],
        ],
      ),
    );
  }
}

/// One machine, fully shown: status, name, serial, checklist progress, any
/// certificate number or earlier return note, and a "Preview" link to its
/// certificate PDF.
class _MachineSummaryCard extends StatelessWidget {
  final InspectionRequest request;
  final MachineSummary machine;

  const _MachineSummaryCard({required this.request, required this.machine});

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
    final locale = Localizations.localeOf(context).languageCode;
    final progress = machine.checklistTotal == 0
        ? 0.0
        : (machine.checklistAnswered / machine.checklistTotal).clamp(0.0, 1.0);
    final kind = [
      machine.type,
      machine.templateName,
    ].whereType<String>().join(' · ');
    String date(String raw) {
      final parsed = DateTime.tryParse(raw);
      return parsed == null
          ? raw
          : DateFormat('d MMM yyyy', locale).format(parsed.toLocal());
    }

    // The certificate's important data, as printed on it.
    final facts = <(String, String)>[
      if (machine.capacity != null) (t.capacityLabel, machine.capacity!),
      if (machine.manufacturer != null)
        (t.manufacturerLabel, machine.manufacturer!),
      if (machine.model != null) (t.modelYearLabel, machine.model!),
      if (machine.inspectionStandards.isNotEmpty)
        (t.machineHeaderStandards, machine.inspectionStandards.join(', ')),
      if (machine.examinationDate != null)
        (t.examinationDateLabel, date(machine.examinationDate!)),
      if (machine.nextExaminationDate != null)
        (t.nextExaminationDateLabel, date(machine.nextExaminationDate!)),
    ];

    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              MachineStatusChip(status: machine.status),
              if (machine.result != null) ...[
                const SizedBox(width: 8),
                _ResultChip(result: machine.result!),
              ],
              const Spacer(),
              Text(
                t.machineQuantityLabel(machine.quantity),
                style: AppTextStyles.caption,
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(machine.name, style: AppTextStyles.cardTitle),
          if (kind.isNotEmpty) ...[
            const SizedBox(height: 2),
            Text(kind, style: AppTextStyles.caption.copyWith(height: 18 / 13)),
          ],
          if (machine.serialNumber != null) ...[
            const SizedBox(height: 2),
            Text(
              machine.serialNumber!,
              style: AppTextStyles.caption.copyWith(height: 18 / 13),
            ),
          ],
          if (facts.isNotEmpty) ...[
            const SizedBox(height: 14),
            for (final (label, value) in facts)
              _Fact(label: label, value: value),
          ],
          if (machine.checklistTotal > 0) ...[
            const SizedBox(height: 14),
            ClipRRect(
              borderRadius: BorderRadius.circular(999),
              child: LinearProgressIndicator(
                value: progress,
                minHeight: 6,
                color: AppColours.primaryColor,
                backgroundColor: AppColours.primaryTint,
              ),
            ),
            const SizedBox(height: 6),
            Text(
              t.machineChecklistProgress(
                machine.checklistAnswered,
                machine.checklistTotal,
              ),
              style: AppTextStyles.caption,
            ),
          ],
          if (machine.certificateNumber != null) ...[
            const SizedBox(height: 10),
            Text(
              t.machineCertificateNumber(machine.certificateNumber!),
              style: AppTextStyles.caption.copyWith(
                color: AppColours.successText,
                fontWeight: FontWeight.w600,
              ),
            ),
          ],
          if (machine.reviewNote != null) ...[
            const SizedBox(height: 12),
            ReviewNote(note: machine.reviewNote!),
          ],
          const SizedBox(height: 12),
          Align(
            alignment: AlignmentDirectional.centerEnd,
            child: InkWell(
              onTap: () => _preview(context),
              borderRadius: BorderRadius.circular(8),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 6),
                child: Text(
                  t.previewAction,
                  style: AppTextStyles.link.copyWith(fontSize: 14),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The certificate's verdict: satisfactory in green, unsatisfactory in red.
class _ResultChip extends StatelessWidget {
  final String result;

  const _ResultChip({required this.result});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (background, textColor, label) = switch (result.toLowerCase()) {
      'satisfactory' => (
        AppColours.chipGreenBackground,
        AppColours.chipGreenText,
        t.resultSatisfactory,
      ),
      'unsatisfactory' => (
        AppColours.chipRedBackground,
        AppColours.chipRedText,
        t.resultUnsatisfactory,
      ),
      _ => (AppColours.chipGreyBackground, AppColours.chipGreyText, result),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: textColor)),
    );
  }
}

/// One label / value line of a machine's important data.
class _Fact extends StatelessWidget {
  final String label;
  final String value;

  const _Fact({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(flex: 2, child: Text(label, style: AppTextStyles.caption)),
          const SizedBox(width: 12),
          Expanded(flex: 3, child: Text(value, style: AppTextStyles.body)),
        ],
      ),
    );
  }
}

/// Why the job can't be approved yet, worded by the API — e.g. an inspector
/// who hasn't filled in a machine's certificate.
class _BlockersCard extends StatelessWidget {
  final List<String> blockers;

  const _BlockersCard({required this.blockers});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColours.chipAmberBackground,
        borderRadius: BorderRadius.circular(18),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            t.reviewBlockersTitle,
            style: AppTextStyles.badge.copyWith(
              color: AppColours.chipAmberTextStrong,
            ),
          ),
          const SizedBox(height: 6),
          for (final blocker in blockers)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                blocker,
                style: AppTextStyles.body.copyWith(
                  color: AppColours.chipAmberText,
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// Earlier comments on the job's review, newest first.
class _HistoryCard extends StatelessWidget {
  final List<ReviewComment> history;

  const _HistoryCard({required this.history});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(t.reviewHistoryTitle, style: AppTextStyles.badge),
          for (final comment in history) ...[
            const SizedBox(height: 10),
            Text(comment.text, style: AppTextStyles.body),
            if (comment.author != null || comment.at != null)
              Text(
                [
                  ?comment.author,
                  if (comment.at != null)
                    DateFormat(
                      'd MMM yyyy · HH:mm',
                      locale,
                    ).format(comment.at!.toLocal()),
                ].join(' · '),
                style: AppTextStyles.caption,
              ),
          ],
        ],
      ),
    );
  }
}

/// "Reject with notes" and "Approve all" for the whole job. Each shows only
/// when the API allows it; when approving isn't allowed the blockers above
/// say why.
class _DecisionBar extends StatelessWidget {
  const _DecisionBar();

  Future<void> _confirmApprove(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<ReviewSummaryCubit>();
    final name = (getIt<AuthCubit>().user?.name ?? '').trim();
    final confirmed = await showAdaptiveDialog<bool>(
      context: context,
      builder: (context) => AlertDialog.adaptive(
        title: Text(t.confirmApproveAllTitle),
        content: Text(t.confirmApproveMessage(name)),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(t.cancel),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(t.approveConfirmAction),
          ),
        ],
      ),
    );
    if (confirmed == true) await cubit.approveAll();
  }

  Future<void> _reject(BuildContext context) async {
    final cubit = context.read<ReviewSummaryCubit>();
    final note = await ReturnNoteDialog.show(context);
    if (note != null) await cubit.rejectAll(note);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<ReviewSummaryCubit, ReviewSummaryState>(
      builder: (context, state) {
        final ready = state.status == ReviewSummaryStatus.ready;
        final showReject = ready && state.canReview;
        final showApprove = ready && state.canReview && state.canApprove;
        // Nothing to decide (already decided, or not this manager's to
        // review): no bar at all.
        if (!showReject && !showApprove) return const SizedBox.shrink();
        return Container(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: AppColours.borderSubtle)),
          ),
          child: SafeArea(
            top: false,
            minimum: const EdgeInsets.only(bottom: 6),
            child: Row(
              children: [
                if (showReject)
                  Expanded(
                    flex: 2,
                    child: SizedBox(
                      height: 54,
                      child: OutlinedButton(
                        onPressed: state.isBusy ? null : () => _reject(context),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: AppColours.dangerText,
                          side: const BorderSide(
                            color: AppColours.dangerBorder,
                          ),
                          textStyle: AppTextStyles.buttonLabel,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(14),
                          ),
                        ),
                        child: state.isReturning
                            ? const SizedBox.square(
                                dimension: 22,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2.5,
                                ),
                              )
                            : Text(
                                t.rejectWithNotesAction,
                                textAlign: TextAlign.center,
                              ),
                      ),
                    ),
                  ),
                if (showReject && showApprove) const SizedBox(width: 10),
                if (showApprove)
                  Expanded(
                    flex: 2,
                    child: MPrimaryButton(
                      label: t.approveAllAction,
                      loading: state.isApproving,
                      onPressed: state.isBusy
                          ? null
                          : () => _confirmApprove(context),
                    ),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}
