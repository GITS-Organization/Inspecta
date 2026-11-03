import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../certificate/presentation/pages/certificate_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../../../technical_manager/presentation/pages/certificate_review_page.dart';
import '../../domain/entities/job_machines.dart';
import '../../domain/entities/machine_status.dart';
import '../../domain/usecases/get_machine_pdf.dart';
import '../bloc/job_machines_cubit.dart';
import '../widgets/machine_widgets.dart';


/// What the skeleton cards are drawn from; only its shape matters.
const _placeholder = MachineSummary(
  requestItemId: 'placeholder',
  name: 'Overhead crane',
  quantity: 1,
  status: MachineStatus.draft,
  serialNumber: 'SN-000000',
  capacity: '10 Tons',
  manufacturer: 'Manufacturer',
  model: 'Model',
  checklistTotal: 20,
  checklistAnswered: 8,
  inspectionStandards: ['BS EN ISO 0000:2011', 'BS ISO 0000:2002'],
);

/// What the skeleton's summary card is drawn from; only its shape matters.
final _placeholderJob = JobMachines(
  requestId: 'placeholder',
  requestStatus: 'assigned',
  clientName: 'Client name',
  location: 'Location of the site',
  scheduledAt: DateTime(2026, 1, 1, 9, 30),
  progress: const MachineProgress(total: 1),
  machines: const [_placeholder],
);

/// A job's machines: the progress of their certificates, a "Start inspection"
/// button when the inspector may begin, and one card per machine to open its
/// form.
///
/// With [pdfOnly] a machine opens its certificate PDF instead of its form —
/// for certificates that are already approved and sent.
class JobMachinesPage extends StatelessWidget {
  final InspectionRequest request;
  final bool pdfOnly;

  const JobMachinesPage({super.key, required this.request, this.pdfOnly = false});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocProvider(
      create: (_) => getIt<JobMachinesCubit>(param1: request.id)..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: Scaffold(
          backgroundColor: AppColours.background,
          body: Column(
            children: [
              MachinesHeader(
                eyebrow: request.clientName.isEmpty ? request.id : request.clientName,
                title: t.machinesTitle,
              ),
              const SizedBox(height: 12),
              Expanded(child: _Body(request: request, pdfOnly: pdfOnly)),
            ],
          ),
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  final InspectionRequest request;
  final bool pdfOnly;

  const _Body({required this.request, required this.pdfOnly});

  /// The machine's certificate as the API renders it (the stored final file
  /// once approved), in the full-screen preview.
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

  // Opens the machine's certificate check list — or, for a technical manager
  // who may review it, the review screen. Submitting, approving or returning
  // moves the machine on, so the machines reload once the user is back.
  Future<void> _open(BuildContext context, MachineSummary machine) async {
    final cubit = context.read<JobMachinesCubit>();
    final review = machine.canReview;
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => review
            ? CertificateReviewPage(
                request: request,
                requestItemId: machine.requestItemId,
                inspectorName: request.inspectorName,
              )
            : CertificatePage(request: request, requestItemId: machine.requestItemId),
      ),
    );
    cubit.start();
  }

  /// A machine's checklist opens only once the inspection has been started.
  static bool _isLocked(JobMachines job, MachineSummary machine) =>
      job.canStartInspection && machine.status == MachineStatus.notStarted;

  Future<void> _start(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final failure = await context.read<JobMachinesCubit>().startInspection();
    if (failure != null) MToast.showError(message: failure.message(t));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocBuilder<JobMachinesCubit, JobMachinesState>(
      builder: (context, state) {
        final job = state.machines;
        if (state.status == JobMachinesStatus.loading || job == null) {
          if (state.status == JobMachinesStatus.error) return _ErrorState(failure: state.failure!);
          // Until the job is read, the steps show as they will when it has not
          // been started: its status tells, so the skeleton is the right shape.
          return _Skeleton(showSteps: !pdfOnly && request.status != JobStatus.inProgress);
        }

        return Column(
          children: [
            Expanded(
              child: RefreshIndicator(
                color: AppColours.primaryColor,
                onRefresh: context.read<JobMachinesCubit>().start,
                child: ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: EdgeInsets.fromLTRB(
                    16,
                    4,
                    16,
                    // The Start bar below carries its own spacing; without it
                    // the list clears the bottom navigation.
                    job.canStartInspection ? 16 : AppLayout.bottomNavClearance,
                  ),
                  children: [
                    _Summary(job: job, fallbackLocation: request.location, showClient: request.clientName.isEmpty),
                    const SizedBox(height: 12),
                    // Start first, then the checklists: the two steps, with the
                    // second one locked until the first is done.
                    if (job.canStartInspection) ...[
                      const _StartSteps(),
                      const SizedBox(height: 12),
                    ],
                    if (job.machines.isEmpty)
                      SizedBox(height: 320, child: EmptyState(title: t.machinesEmpty))
                    else
                      for (final machine in job.machines) ...[
                        MachineCard(
                          machine: machine,
                          locked: _isLocked(job, machine),
                          onTap: () => _isLocked(job, machine)
                              ? MToast.showInfo(message: t.machineStartFirstHint)
                              : pdfOnly
                              ? _viewPdf(context, machine)
                              : _open(context, machine),
                          onViewPdf: pdfOnly ? () => _viewPdf(context, machine) : null,
                        ),
                        const SizedBox(height: 12),
                      ],
                  ],
                ),
              ),
            ),
            // The one thing the inspector does first sits in the thumb zone,
            // like the footers of the other work screens.
            if (job.canStartInspection)
              Container(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 16),
                decoration: const BoxDecoration(
                  color: Colors.white,
                  border: Border(top: BorderSide(color: AppColours.borderSubtle)),
                ),
                child: SafeArea(
                  top: false,
                  minimum: const EdgeInsets.only(bottom: 6),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      MPrimaryButton(
                        label: t.startInspectionAction,
                        loading: state.isStarting,
                        onPressed: () => _start(context),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        t.startInspectionHint,
                        textAlign: TextAlign.center,
                        style: AppTextStyles.caption.copyWith(fontSize: 12),
                      ),
                    ],
                  ),
                ),
              ),
          ],
        );
      },
    );
  }
}

/// What the inspector does, in order, before any checklist: start the
/// inspection (step 1, now), then fill in each machine (step 2, locked until
/// step 1 is done). Shown only while the inspection hasn't started.
class _StartSteps extends StatelessWidget {
  const _StartSteps();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          _Step(
            number: 1,
            active: true,
            title: t.inspectionStepStartTitle,
            hint: t.inspectionStepStartHint,
          ),
          Container(
            margin: const EdgeInsetsDirectional.only(start: 13, top: 4, bottom: 4),
            width: 2,
            height: 14,
            color: AppColours.borderSubtle,
          ),
          _Step(
            number: 2,
            active: false,
            title: t.inspectionStepFillTitle,
            hint: t.inspectionStepFillHint,
          ),
        ],
      ),
    );
  }
}

/// One step of [_StartSteps]: a numbered circle (a lock while it can't be
/// done yet), its title and a line of what it means.
class _Step extends StatelessWidget {
  final int number;
  final bool active;
  final String title;
  final String hint;

  const _Step({required this.number, required this.active, required this.title, required this.hint});

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Skeleton.leaf(
          child: Container(
            width: 28,
            height: 28,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: active ? AppColours.primaryColor : AppColours.surfaceMuted,
              shape: BoxShape.circle,
            ),
            child: active
                ? Text(
                    '$number',
                    style: AppTextStyles.badge.copyWith(color: Colors.white, fontWeight: FontWeight.w800),
                  )
                : const Icon(Icons.lock_outline_rounded, size: 15, color: AppColours.inkMuted),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                title,
                style: AppTextStyles.cardTitle.copyWith(
                  fontSize: 15,
                  color: active ? null : AppColours.inkMuted,
                ),
              ),
              const SizedBox(height: 1),
              Text(hint, style: AppTextStyles.caption.copyWith(height: 18 / 13)),
            ],
          ),
        ),
      ],
    );
  }
}

/// Where and when the job is, and how far its machines have got.
///
/// The client's name is in the header above, so it is repeated here only
/// when the header has nothing better than the request id to show
/// ([showClient]).
class _Summary extends StatelessWidget {
  final JobMachines job;
  final String fallbackLocation;
  final bool showClient;

  const _Summary({required this.job, required this.fallbackLocation, required this.showClient});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final location = job.location.isEmpty ? fallbackLocation : job.location;
    final scheduledAt = job.scheduledAt?.toLocal();
    final progress = job.progress;
    final client = showClient ? job.clientName : '';
    final hasDetails = client.isNotEmpty || location.isNotEmpty || scheduledAt != null;

    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (client.isNotEmpty) ...[
            Text(client, style: AppTextStyles.cardTitle),
            const SizedBox(height: 4),
          ],
          // Where and when, on one line.
          if (location.isNotEmpty || scheduledAt != null)
            Wrap(
              spacing: 16,
              runSpacing: 6,
              children: [
                if (location.isNotEmpty)
                  _MetaItem(icon: Icons.location_on_outlined, label: location),
                if (scheduledAt != null)
                  _MetaItem(
                    icon: Icons.event_outlined,
                    label: DateFormat('EEE, d MMM · HH:mm', locale).format(scheduledAt),
                  ),
              ],
            ),
          if (hasDetails) const SizedBox(height: 12),
          Row(
            children: [
              Text(
                t.certificatesLabel,
                style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w600, color: AppColours.inkSecondary),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  t.machinesProgress(progress.submitted, progress.total),
                  style: AppTextStyles.caption,
                  textAlign: TextAlign.end,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          _ProgressSegments(machines: job.machines, progress: progress),
        ],
      ),
    );
  }
}

/// An icon and a short line of the summary card's meta row.
class _MetaItem extends StatelessWidget {
  final IconData icon;
  final String label;

  const _MetaItem({required this.icon, required this.label});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 16, color: AppColours.inkMuted),
        const SizedBox(width: 6),
        Flexible(
          child: Text(
            label,
            style: AppTextStyles.subtitle.copyWith(fontSize: 14, color: AppColours.inkBody),
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    );
  }
}

/// The job's progress as one segment per machine, each coloured by where
/// that machine stands — green once submitted (or beyond), blue while being
/// filled in, red when sent back, pale when untouched — so a job with
/// several machines shows which ones are left, not just how many. Past
/// [_maxSegments] machines the segments would be slivers, so it falls back
/// to a single bar of the submitted share.
class _ProgressSegments extends StatelessWidget {
  static const _maxSegments = 16;

  final List<MachineSummary> machines;
  final MachineProgress progress;

  const _ProgressSegments({required this.machines, required this.progress});

  static Color _colorOf(MachineStatus status) => switch (status) {
    MachineStatus.submitted || MachineStatus.approved || MachineStatus.delivered => AppColours.successIcon,
    MachineStatus.returned => AppColours.errorIcon,
    MachineStatus.draft => AppColours.primaryColor.withValues(alpha: 0.45),
    MachineStatus.notStarted => AppColours.primaryTint,
  };

  @override
  Widget build(BuildContext context) => Skeleton.replace(
    replacement: const Bone(width: double.infinity, height: 8, uniRadius: 4),
    child: _bar(),
  );

  Widget _bar() {
    if (machines.isEmpty || machines.length > _maxSegments) {
      final value = progress.total == 0 ? 0.0 : (progress.submitted / progress.total).clamp(0.0, 1.0);
      return ClipRRect(
        borderRadius: BorderRadius.circular(999),
        child: LinearProgressIndicator(
          value: value,
          minHeight: 8,
          color: AppColours.successIcon,
          backgroundColor: AppColours.primaryTint,
        ),
      );
    }

    return Row(
      children: [
        for (final (index, machine) in machines.indexed)
          Expanded(
            child: Container(
              height: 8,
              margin: EdgeInsetsDirectional.only(end: index == machines.length - 1 ? 0 : 4),
              decoration: BoxDecoration(color: _colorOf(machine.status), borderRadius: BorderRadius.circular(999)),
            ),
          ),
      ],
    );
  }
}

/// The loading state, in the shape of the screen: the summary card, the two
/// steps (while the inspection hasn't started — [showSteps]) and item cards,
/// all drawn by the real widgets from placeholders, shimmering.
class _Skeleton extends StatelessWidget {
  final bool showSteps;

  const _Skeleton({required this.showSteps});

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: ListView(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 4, 16, 0),
        children: [
          _Summary(job: _placeholderJob, fallbackLocation: '', showClient: false),
          const SizedBox(height: 12),
          if (showSteps) ...[
            const _StartSteps(),
            const SizedBox(height: 12),
          ],
          for (var i = 0; i < 2; i++) ...[
            MachineCard(machine: _placeholder, locked: showSteps, onTap: () {}),
            const SizedBox(height: 12),
          ],
        ],
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  final RequestsFailureCode failure;

  const _ErrorState({required this.failure});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            MNotice(type: MNoticeType.error, message: failure.message(t)),
            const SizedBox(height: 16),
            OutlinedButton(onPressed: context.read<JobMachinesCubit>().retry, child: Text(t.retry)),
          ],
        ),
      ),
    );
  }
}
