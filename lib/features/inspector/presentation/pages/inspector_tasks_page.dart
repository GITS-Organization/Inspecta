import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../../../machines/presentation/pages/job_machines_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_source.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/inspector_tasks_cubit.dart';
import '../widgets/inspector_task_card.dart';
import '../widgets/inspector_tasks_filters.dart';

/// Feature 05's Tasks tab: Today, Upcoming and Overdue lists (chosen from the
/// filter in the header) and the task cards, grouped under the day they are
/// scheduled for.
class InspectorTasksPage extends StatelessWidget {
  final VoidCallback? onOpenProfile;

  const InspectorTasksPage({super.key, this.onOpenProfile});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<InspectorTasksCubit>()..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: Scaffold(
          backgroundColor: AppColours.background,
          body: Column(
            children: [
              UserAppHeader(
                title: AppLocalizations.of(context)!.navTasks,
                onOpenProfile: onOpenProfile,
                bottom: const InspectorTasksFilters(),
              ),
              const Expanded(child: _Body()),
            ],
          ),
        ),
      ),
    );
  }
}

class _Body extends StatefulWidget {
  const _Body();

  @override
  State<_Body> createState() => _BodyState();
}

class _BodyState extends State<_Body> {
  final Map<String, GlobalKey> _cardKeys = {};

  GlobalKey _keyFor(String requestId) => _cardKeys.putIfAbsent(requestId, GlobalKey.new);

  Future<void> _openInMaps(InspectionRequest task) async {
    final uri = Uri.parse(
      'https://www.google.com/maps/search/?api=1&query=${Uri.encodeComponent(task.location)}',
    );
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  }

  // The machines screen may start the inspection, which moves the job on, so
  // the list reloads once the user is back.
  Future<void> _open(InspectionRequest task) async {
    final cubit = context.read<InspectorTasksCubit>();
    await Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => JobMachinesPage(request: task)),
    );
    cubit.start();
  }

  Widget _empty(AppLocalizations t, TasksFilter filter) => switch (filter) {
    TasksFilter.today => EmptyState(icon: Icons.event_available_rounded, title: t.emptyTasksToday),
    TasksFilter.upcoming => EmptyState(icon: Icons.event_note_rounded, title: t.emptyTasksUpcoming),
    TasksFilter.overdue => EmptyState(kind: EmptyKind.done, title: t.emptyTasksOverdue),
  };

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<InspectorTasksCubit, InspectorTasksState>(
      builder: (context, state) {
        if (state.status == InspectorTasksStatus.loading) {
          return const _Skeleton();
        }
        if (state.status == InspectorTasksStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final tasks = state.visibleTasks;
        final cubit = context.read<InspectorTasksCubit>();

        return RefreshIndicator(
          color: AppColours.primaryColor,
          onRefresh: cubit.start,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(16, 16, 16, AppLayout.bottomNavClearance),
            children: [
              if (tasks.isEmpty)
                _empty(t, state.filter)
              else
                for (var i = 0; i < tasks.length; i++) ...[
                  // Tasks sit under the day they are scheduled for: one
                  // heading per day, however many tasks it has.
                  if (i == 0 || !DateUtils.isSameDay(tasks[i - 1].scheduledAt, tasks[i].scheduledAt)) ...[
                    if (i > 0) const SizedBox(height: 8),
                    _DayHeading(
                      day: tasks[i].scheduledAt!,
                      filter: state.filter,
                      overdueDays: InspectorTasksState.daysOverdue(tasks[i]),
                    ),
                    const SizedBox(height: 12),
                  ] else
                    const SizedBox(height: 12),
                  KeyedSubtree(
                    key: _keyFor(tasks[i].id),
                    child: InspectorTaskCard(
                      task: tasks[i],
                      onOpen: () => _open(tasks[i]),
                      onNavigate: () => _openInMaps(tasks[i]),
                    ),
                  ),
                ],
            ],
          ),
        );
      },
    );
  }
}

/// The heading over a day's tasks: "Today · Tue, 3 Nov", "Tomorrow", or a
/// date — with, for a late day, one "2 days overdue" tag instead of one on
/// every card.
class _DayHeading extends StatelessWidget {
  final DateTime day;
  final TasksFilter filter;
  final int overdueDays;

  const _DayHeading({required this.day, required this.filter, required this.overdueDays});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    final tomorrow = DateTime.now().add(const Duration(days: 1));
    final date = DateFormat('EEE, d MMM', locale).format(day);

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 4),
      child: Row(
        children: [
          if (filter == TasksFilter.today) ...[
            Text(t.todayLabel, style: AppTextStyles.sectionTitle),
            const SizedBox(width: 6),
            Text(
              '· $date',
              style: AppTextStyles.subtitle.copyWith(fontSize: 15, color: AppColours.inkMuted),
            ),
          ] else
            Text(
              filter == TasksFilter.upcoming && DateUtils.isSameDay(day, tomorrow) ? t.tomorrowLabel : date,
              style: AppTextStyles.sectionTitle,
            ),
          if (overdueDays > 0) ...[
            const SizedBox(width: 10),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(color: AppColours.chipRedBackground, borderRadius: BorderRadius.circular(999)),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.schedule_rounded, size: 14, color: AppColours.chipRedText),
                  const SizedBox(width: 4),
                  Text(
                    t.taskOverdueByDays(overdueDays),
                    style: AppTextStyles.badge.copyWith(color: AppColours.chipRedText),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// What the skeleton's cards are drawn from; only their shape matters.
final _placeholder = InspectionRequest(
  id: 'placeholder',
  source: RequestSource.email,
  clientName: 'Client name',
  subject: 'QT-2026-000000 rev01',
  receivedAt: DateTime(2026),
  location: 'Location of the site',
  status: JobStatus.assigned,
  items: const [],
  scheduledAt: DateTime(2026, 1, 1, 9, 30),
  certificates: const CertificateCounts(total: 1),
);

/// The loading state, in the shape of the list: a day heading and two task
/// cards, drawn by the real widgets from [_placeholder], shimmering.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: ListView(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
        children: [
          _DayHeading(day: _placeholder.scheduledAt!, filter: TasksFilter.today, overdueDays: 0),
          const SizedBox(height: 12),
          for (var i = 0; i < 2; i++) ...[
            InspectorTaskCard(task: _placeholder, onOpen: () {}, onNavigate: () {}),
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
            OutlinedButton(
              onPressed: context.read<InspectorTasksCubit>().retry,
              child: Text(t.retry),
            ),
          ],
        ),
      ),
    );
  }
}
