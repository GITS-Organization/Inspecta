import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_back_button.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../core/widgets/app_date_picker.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/domain/entities/qualification.dart';
import '../../../auth/domain/entities/user.dart';
import '../../../auth/domain/entities/user_role.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/presentation/widgets/job_stepper.dart';
import '../bloc/assign_inspector_cubit.dart';
import '../widgets/coordinator_labels.dart';
import '../widgets/inspector_option_tile.dart';

/// Feature 04 §5's "Assign inspector" screen: pick a time slot, choose an
/// inspector, and submit — the only way to move a job from
/// [InspectionRequest.isReadyToAssign] to [JobStatus.assigned].
class AssignInspectorPage extends StatelessWidget {
  final InspectionRequest request;
  final String? preselectedInspectorId;

  const AssignInspectorPage({super.key, required this.request, this.preselectedInspectorId});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) =>
          getIt<AssignInspectorCubit>(param1: request, param2: preselectedInspectorId)..start(),
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            _Header(request: request),
            Expanded(child: _Body(request: request)),
            _Footer(request: request),
          ],
        ),
      ),
    );
  }
}

/// The banner: back button, request id and title, and the job's progress row
/// shown one step on — Request and Quote done, Assign now.
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
                    _CopyableId(request.id),
                    const SizedBox(height: 2),
                    Text(
                      t.assignInspectorTitle,
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
          // Request and Quote are done; this screen is the Assign step.
          const JobStepper(status: JobStatus.assigned, currentStep: 2),
        ],
      ),
    );
  }
}

/// The request id under the back button, as "#id" with a copy icon: tapping
/// it puts the id on the clipboard.
class _CopyableId extends StatelessWidget {
  final String id;

  const _CopyableId(this.id);

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return InkWell(
      borderRadius: BorderRadius.circular(6),
      onTap: () async {
        await Clipboard.setData(ClipboardData(text: id));
        MToast.showSuccess(message: t.idCopiedMessage);
      },
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Flexible(
            child: Text(
              '#$id',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppTextStyles.caption.copyWith(
                fontWeight: FontWeight.w600,
                letterSpacing: 0.24,
                height: 16 / 12,
                fontSize: 12,
              ),
            ),
          ),
          const SizedBox(width: 4),
          const Icon(Icons.copy_rounded, size: 13, color: AppColours.inkMuted),
        ],
      ),
    );
  }
}

/// A numbered step card of the form: a round navy badge with the step's
/// number, its [title], an optional [trailing] chip, then the [child].
class _StepCard extends StatelessWidget {
  final int number;
  final String title;
  final Widget? trailing;
  final Widget child;

  const _StepCard({required this.number, required this.title, required this.child, this.trailing});

  @override
  Widget build(BuildContext context) {
    return MCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Container(
                width: 28,
                height: 28,
                alignment: Alignment.center,
                decoration: const BoxDecoration(color: AppColours.navy, shape: BoxShape.circle),
                child: Text(
                  '$number',
                  style: const TextStyle(fontSize: 13, height: 1, fontWeight: FontWeight.w700, color: Colors.white),
                ),
              ),
              const SizedBox(width: 12),
              Flexible(child: Text(title, style: AppTextStyles.sectionTitle)),
              if (trailing != null) ...[const SizedBox(width: 10), trailing!],
            ],
          ),
          const SizedBox(height: 16),
          child,
        ],
      ),
    );
  }
}

class _Body extends StatelessWidget {
  final InspectionRequest request;

  const _Body({required this.request});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final categories = request.items
        .map((item) => item.category)
        .whereType<String>()
        .where((c) => c.isNotEmpty)
        .toSet();

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
      children: [
        // The job being assigned.
        MCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                children: [
                  Container(
                    width: 48,
                    height: 48,
                    decoration: BoxDecoration(
                      color: AppColours.primaryTint,
                      borderRadius: BorderRadius.circular(14),
                    ),
                    child: const Icon(
                      Icons.precision_manufacturing_outlined,
                      size: 22,
                      color: AppColours.primaryColor,
                    ),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(request.equipmentTitle, style: AppTextStyles.cardTitle),
                        const SizedBox(height: 2),
                        Text(
                          '${request.clientName} · ${request.location}',
                          style: AppTextStyles.caption.copyWith(height: 18 / 13),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              if (categories.isNotEmpty || request.preferredDate != null) ...[
                const SizedBox(height: 16),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    for (final category in categories)
                      _Tag(
                        label: category,
                        background: AppColours.primaryTint,
                        color: AppColours.primaryDark,
                      ),
                    if (request.preferredDate != null)
                      _Tag(
                        label: request.preferredDate!.dueLabel(t),
                        background: AppColours.chipAmberBackground,
                        color: AppColours.chipAmberText,
                      ),
                  ],
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: 12),
        // When.
        _StepCard(
          number: 1,
          title: t.pickTimeTitle,
          child: BlocBuilder<AssignInspectorCubit, AssignInspectorState>(
                buildWhen: (previous, current) => previous.scheduledAt != current.scheduledAt,
                builder: (context, state) {
                  final cubit = context.read<AssignInspectorCubit>();
                  return Row(
                    children: [
                      Expanded(
                        flex: 2,
                        child: _PickerField(
                          label: t.dateLabel,
                          icon: Icons.calendar_today_outlined,
                          value: DateFormat(
                            'EEE, d MMM yyyy',
                            Localizations.localeOf(context).languageCode,
                          ).format(state.scheduledAt),
                          onTap: () async {
                            final picked = await AppDatePicker.pickDate(
                              context,
                              title: t.dateLabel,
                              initialDate: state.scheduledAt,
                              // Wide enough to always include the initial date —
                              // a request's preferred date can already be in the
                              // past (an overdue job still needs assigning).
                              firstDate: DateTime(DateTime.now().year - 1),
                              lastDate: DateTime(DateTime.now().year + 2),
                            );
                            if (picked != null) cubit.changeDate(picked);
                          },
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: _PickerField(
                          label: t.startTimeLabel,
                          icon: Icons.schedule_rounded,
                          value: DateFormat(
                            'HH:mm',
                            Localizations.localeOf(context).languageCode,
                          ).format(state.scheduledAt),
                          onTap: () async {
                            final picked = await showTimePicker(
                              context: context,
                              initialTime: TimeOfDay.fromDateTime(state.scheduledAt),
                            );
                            if (picked != null) cubit.changeTime(picked.hour, picked.minute);
                          },
                        ),
                      ),
                    ],
                  );
                },
              ),
        ),
        const SizedBox(height: 12),
        // Who.
        _StepCard(
          number: 2,
          title: t.chooseInspectorTitle,
          child: BlocBuilder<AssignInspectorCubit, AssignInspectorState>(
                builder: (context, state) {
                  final cubit = context.read<AssignInspectorCubit>();
                  if (state.inspectorsStatus == InspectorsStatus.loading) {
                    return const _InspectorsSkeleton();
                  }
                  if (state.inspectorsStatus == InspectorsStatus.error) {
                    return MNotice(type: MNoticeType.error, message: state.inspectorsFailure!.message(t));
                  }
                  if (state.inspectors.isEmpty) {
                    return EmptyState(title: t.emptyInspectors, compact: true);
                  }
                  return Column(
                    children: [
                      for (var i = 0; i < state.inspectors.length; i++) ...[
                        InspectorOptionTile(
                          inspector: state.inspectors[i],
                          selected: state.inspectors[i].id == state.selectedInspectorId,
                          isBestMatch: state.matchedInspectorIds.contains(state.inspectors[i].id),
                          onTap: () => cubit.selectInspector(state.inspectors[i].id),
                        ),
                        if (i < state.inspectors.length - 1) const SizedBox(height: 10),
                      ],
                      if (state.submitted && state.selectedInspectorId == null) ...[
                        const SizedBox(height: 10),
                        Align(
                          alignment: AlignmentDirectional.centerStart,
                          child: Text(
                            t.errorInspectorRequired,
                            style: AppTextStyles.caption.copyWith(
                              color: AppColours.dangerText,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ),
                      ],
                    ],
                  );
                },
              ),
        ),
        const SizedBox(height: 12),
        // Notes.
        _StepCard(
          number: 3,
          title: t.assignNotesTitle,
          trailing: Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
            decoration: BoxDecoration(
              color: AppColours.surfaceMuted,
              borderRadius: BorderRadius.circular(999),
              border: Border.all(color: AppColours.border),
            ),
            child: Text(
              t.optionalChipLabel,
              style: AppTextStyles.badge.copyWith(color: AppColours.inkSecondary),
            ),
          ),
          child: TextField(
                onChanged: context.read<AssignInspectorCubit>().changeNote,
                minLines: 3,
                maxLines: 5,
                cursorColor: AppColours.primaryColor,
                style: AppTextStyles.input.copyWith(fontWeight: FontWeight.w400, fontSize: 14, height: 20 / 14),
                decoration: InputDecoration(
                  isDense: true,
                  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                  filled: true,
                  fillColor: AppColours.surfaceMuted,
                  hintText: t.assignNotesHint,
                  hintStyle: AppTextStyles.input.copyWith(
                    fontWeight: FontWeight.w400,
                    fontSize: 15,
                    height: 20 / 15,
                    color: AppColours.inkMuted,
                  ),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(14),
                    borderSide: const BorderSide(color: AppColours.borderSubtle),
                  ),
                  enabledBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(14),
                    borderSide: const BorderSide(color: AppColours.borderSubtle),
                  ),
                  focusedBorder: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(14),
                    borderSide: const BorderSide(color: AppColours.primaryColor, width: 2),
                  ),
                ),
              ),
        ),
      ],
    );
  }
}

class _Tag extends StatelessWidget {
  final String label;
  final Color background;
  final Color color;

  const _Tag({required this.label, required this.background, required this.color});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(color: background, borderRadius: BorderRadius.circular(999)),
      child: Text(label, style: AppTextStyles.badge.copyWith(color: color)),
    );
  }
}

class _PickerField extends StatelessWidget {
  final String label;
  final String value;
  final IconData icon;
  final VoidCallback onTap;

  const _PickerField({required this.label, required this.value, required this.icon, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
        const SizedBox(height: 6),
        InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: onTap,
          child: Container(
            height: 48,
            padding: const EdgeInsets.symmetric(horizontal: 14),
            decoration: BoxDecoration(
              color: AppColours.surfaceMuted,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: AppColours.borderSubtle),
            ),
            child: Row(
              children: [
                Icon(icon, size: 20, color: AppColours.inkSecondary),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    value,
                    style: AppTextStyles.input.copyWith(fontSize: 14),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _Footer extends StatelessWidget {
  final InspectionRequest request;

  const _Footer({required this.request});

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
        child: BlocConsumer<AssignInspectorCubit, AssignInspectorState>(
          listenWhen: (previous, current) => current.actionSeq != previous.actionSeq,
          listener: (context, state) {
            if (state.lastActionSuccess) {
              MToast.showSuccess(message: t.assignmentSuccessMessage);
              Navigator.of(context).pop();
            } else {
              MToast.showError(message: state.lastActionFailure!.message(t));
            }
          },
          builder: (context, state) {
            final cubit = context.read<AssignInspectorCubit>();
            final name = state.selectedInspector?.name.split(' ').first;
            return MPrimaryButton(
              label: name == null ? t.assignInspectorAction : t.assignAndNotifyAction(name),
              loading: state.isSubmitting,
              onPressed: cubit.submit,
            );
          },
        ),
      ),
    );
  }
}

/// What the skeleton rows are drawn from; only its shape matters.
const _placeholderInspector = AppUser(
  id: 'placeholder',
  name: 'Inspector name',
  email: '',
  role: UserRole.inspector,
  qualifications: [Qualification(name: 'Cranes'), Qualification(name: 'Lifts')],
);

/// The inspector list's loading state: option rows drawn from
/// [_placeholderInspector], shimmering.
class _InspectorsSkeleton extends StatelessWidget {
  const _InspectorsSkeleton();

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: Column(
        children: [
          for (var i = 0; i < 3; i++) ...[
            InspectorOptionTile(
              inspector: _placeholderInspector,
              selected: false,
              isBestMatch: false,
              onTap: () {},
            ),
            if (i < 2) const SizedBox(height: 10),
          ],
        ],
      ),
    );
  }
}
