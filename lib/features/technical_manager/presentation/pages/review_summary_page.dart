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
import '../../../../core/widgets/empty_widget.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../certificate/presentation/widgets/certificate_labels.dart';
import '../../../machines/domain/entities/job_machines.dart';
import '../../../machines/domain/entities/machine_status.dart';
import '../../../machines/presentation/widgets/machine_widgets.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_source.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/review_summary_cubit.dart';
import '../widgets/return_note_dialog.dart';
import '../widgets/review_machine_ticket.dart';

/// What the technical manager opens from a Review-tab card: the job's
/// `to-review` row, then one ticket per machine with the certificate's verdict
/// and a "Preview" button for its PDF, and the two decisions on the whole job
/// — approve every certificate or reject them all with a note.
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
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          children: [
            _JobStrip(request: state.request),
            if (state.reviewHistory.isNotEmpty) ...[
              const SizedBox(height: 12),
              _HistoryCard(history: state.reviewHistory),
            ],
            _SectionHeader(
              title: t.equipmentToReviewTitle,
              count: state.machines.length,
            ),
            if (state.machines.isEmpty)
              SizedBox(height: 240, child: EmptyState(title: t.machinesEmpty))
            else
              for (final machine in state.machines) ...[
                _Rise(
                  child: ReviewMachineTicket(
                    request: state.request,
                    machine: machine,
                  ),
                ),
                const SizedBox(height: 16),
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

/// The loading state: the job strip and a ticket drawn from placeholders,
/// shimmering — as on the machines screen.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  static const _machine = MachineSummary(
    requestItemId: 'placeholder',
    name: 'Overhead crane',
    quantity: 1,
    status: MachineStatus.submitted,
    serialNumber: 'SN-000000',
    checklistTotal: 20,
    checklistAnswered: 20,
    type: 'Overhead crane',
    capacity: '10 Tons',
    manufacturer: 'Manufacturer',
    model: 'Model 0000',
    inspectionStandards: ['BS EN 00000:0000'],
    examinationDate: '2000-01-01',
    nextExaminationDate: '2000-07-01',
    result: 'satisfactory',
  );

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final job = InspectionRequest(
      id: 'placeholder',
      source: RequestSource.email,
      clientName: 'Client name',
      receivedAt: DateTime(2000),
      location: 'City, Country',
      scheduledAt: DateTime(2000),
      status: JobStatus.certificateSubmitted,
      items: const [],
      certificates: const CertificateCounts(total: 2, submitted: 2),
    );
    return Skeletonizer(
      child: ListView(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
        children: [
          _JobStrip(request: job),
          _SectionHeader(title: t.equipmentToReviewTitle, count: 1),
          ReviewMachineTicket(request: job, machine: _machine),
        ],
      ),
    );
  }
}

/// Where and when the job is, and how many of its certificates are in. The
/// client is already named in the header above.
class _JobStrip extends StatelessWidget {
  final InspectionRequest request;

  const _JobStrip({required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final scheduledAt = request.scheduledAt?.toLocal();
    final counts = request.certificates;
    if (request.location.isEmpty && scheduledAt == null && counts == null) {
      return const SizedBox.shrink();
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(22),
        boxShadow: const [
          BoxShadow(color: Color(0x0D0B1A3B), blurRadius: 2, offset: Offset(0, 1)),
        ],
      ),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                if (request.location.isNotEmpty)
                  _StripLine(
                    icon: Icons.location_on_outlined,
                    text: request.location,
                  ),
                if (request.location.isNotEmpty && scheduledAt != null)
                  const SizedBox(height: 8),
                if (scheduledAt != null)
                  _StripLine(
                    icon: Icons.event_outlined,
                    text: DateFormat(
                      'EEE, d MMM · HH:mm',
                      locale,
                    ).format(scheduledAt),
                  ),
              ],
            ),
          ),
          if (counts != null) ...[
            const SizedBox(width: 12),
            _SubmittedPill(
              label: t.machinesProgress(counts.submitted, counts.total),
              complete: counts.total > 0 && counts.submitted >= counts.total,
            ),
          ],
        ],
      ),
    );
  }
}

class _StripLine extends StatelessWidget {
  final IconData icon;
  final String text;

  const _StripLine({required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 16, color: AppColours.inkSecondary),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            text,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.subtitle.copyWith(
              height: 20 / 14,
              fontWeight: FontWeight.w500,
              color: AppColours.inkBody,
            ),
          ),
        ),
      ],
    );
  }
}

/// "1 of 1 submitted": green once every certificate is in, amber until then.
class _SubmittedPill extends StatelessWidget {
  final String label;
  final bool complete;

  const _SubmittedPill({required this.label, required this.complete});

  @override
  Widget build(BuildContext context) {
    final background = complete
        ? AppColours.successBackground
        : AppColours.chipAmberBackground;
    final foreground = complete
        ? AppColours.successText
        : AppColours.chipAmberText;
    return Container(
      height: 32,
      padding: EdgeInsetsDirectional.fromSTEB(complete ? 10 : 12, 0, 12, 0),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (complete) ...[
            Icon(Icons.check_rounded, size: 14, color: foreground),
            const SizedBox(width: 6),
          ],
          Text(
            label,
            maxLines: 1,
            style: AppTextStyles.caption.copyWith(
              height: 1,
              fontWeight: FontWeight.w600,
              color: foreground,
            ),
          ),
        ],
      ),
    );
  }
}

/// "Equipment to review" with a navy count badge.
class _SectionHeader extends StatelessWidget {
  final String title;
  final int count;

  const _SectionHeader({required this.title, required this.count});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(2, 16, 2, 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Expanded(
            child: Text(
              title,
              style: AppTextStyles.sectionTitle.copyWith(
                fontSize: 17,
                letterSpacing: -0.17,
              ),
            ),
          ),
          if (count > 0)
            Container(
              constraints: const BoxConstraints(minWidth: 24),
              height: 24,
              padding: const EdgeInsets.symmetric(horizontal: 7),
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: AppColours.navy,
                borderRadius: BorderRadius.circular(12),
              ),
              child: Text(
                '$count',
                style: AppTextStyles.badge.copyWith(
                  fontSize: 13,
                  height: 1,
                  color: Colors.white,
                ),
              ),
            ),
        ],
      ),
    );
  }
}

/// Lifts its child into place as it first appears, unless the user has turned
/// animations off.
class _Rise extends StatelessWidget {
  final Widget child;

  const _Rise({required this.child});

  @override
  Widget build(BuildContext context) {
    if (MediaQuery.disableAnimationsOf(context)) return child;
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: const Duration(milliseconds: 550),
      curve: const Cubic(0.2, 0.7, 0.2, 1),
      builder: (context, value, child) => Opacity(
        opacity: value,
        child: Transform.translate(
          offset: Offset(0, 12 * (1 - value)),
          child: child,
        ),
      ),
      child: child,
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
        borderRadius: BorderRadius.circular(22),
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
      borderRadius: 22,
      padding: const EdgeInsets.all(16),
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
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
          decoration: const BoxDecoration(
            color: Colors.white,
            border: Border(top: BorderSide(color: AppColours.borderSubtle)),
            boxShadow: [
              BoxShadow(
                color: Color(0x120B1A3B),
                blurRadius: 28,
                offset: Offset(0, -12),
              ),
            ],
          ),
          child: SafeArea(
            top: false,
            minimum: const EdgeInsets.only(bottom: 14),
            child: Row(
              children: [
                if (showReject)
                  Expanded(
                    child: _RejectButton(
                      label: t.rejectWithNotesAction,
                      loading: state.isReturning,
                      onPressed: state.isBusy ? null : () => _reject(context),
                    ),
                  ),
                if (showReject && showApprove) const SizedBox(width: 10),
                if (showApprove)
                  Expanded(
                    child: _ApproveButton(
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

class _RejectButton extends StatelessWidget {
  final String label;
  final bool loading;
  final VoidCallback? onPressed;

  const _RejectButton({
    required this.label,
    required this.loading,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 52,
      child: OutlinedButton(
        onPressed: onPressed,
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColours.dangerText,
          padding: const EdgeInsets.symmetric(horizontal: 8),
          side: const BorderSide(color: AppColours.dangerBorder, width: 1.5),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        ),
        child: loading
            ? const SizedBox.square(
                dimension: 22,
                child: CircularProgressIndicator(
                  strokeWidth: 2.5,
                  color: AppColours.dangerText,
                ),
              )
            : Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.speaker_notes_outlined, size: 16),
                  const SizedBox(width: 7),
                  Flexible(
                    child: Text(
                      label,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.fieldLabel.copyWith(
                        color: AppColours.dangerText,
                      ),
                    ),
                  ),
                ],
              ),
      ),
    );
  }
}

class _ApproveButton extends StatelessWidget {
  final String label;
  final bool loading;
  final VoidCallback? onPressed;

  const _ApproveButton({
    required this.label,
    required this.loading,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    final enabled = onPressed != null;
    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(16),
        boxShadow: enabled || loading
            ? const [
                BoxShadow(
                  color: Color(0x800B1A3B),
                  blurRadius: 18,
                  spreadRadius: -6,
                  offset: Offset(0, 8),
                ),
              ]
            : null,
      ),
      child: SizedBox(
        height: 52,
        child: FilledButton(
          onPressed: onPressed,
          style: FilledButton.styleFrom(
            backgroundColor: AppColours.primaryColor,
            disabledBackgroundColor: AppColours.primaryColor.withValues(
              alpha: loading ? 1 : 0.5,
            ),
            padding: const EdgeInsets.symmetric(horizontal: 8),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
            ),
          ),
          child: loading
              ? const SizedBox.square(
                  dimension: 22,
                  child: CircularProgressIndicator(
                    strokeWidth: 2.5,
                    color: Colors.white,
                  ),
                )
              : Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.check_rounded, size: 18, color: Colors.white),
                    const SizedBox(width: 8),
                    Flexible(
                      child: Text(
                        label,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: AppTextStyles.buttonLabel.copyWith(
                          letterSpacing: 0,
                        ),
                      ),
                    ),
                  ],
                ),
        ),
      ),
    );
  }
}
