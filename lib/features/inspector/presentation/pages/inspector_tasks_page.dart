import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../../../machines/presentation/pages/job_machines_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/inspector_tasks_cubit.dart';
import '../widgets/inspector_task_card.dart';
import '../widgets/inspector_tasks_filters.dart';

/// Feature 05's Tasks tab: Today, Upcoming and Overdue lists (chosen from the
/// header's tiles) and the task cards.
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
              ),
              const SizedBox(height: 12),
              const InspectorTasksFilters(),
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
  Future<void> _continueCertificate(InspectionRequest task) async {
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
          return Loading.loader(context);
        }
        if (state.status == InspectorTasksStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final tasks = state.visibleTasks;
        final cubit = context.read<InspectorTasksCubit>();
        final locale = Localizations.localeOf(context).languageCode;
        final tomorrow = DateTime.now().add(const Duration(days: 1));

        String dayLabel(DateTime day) => DateUtils.isSameDay(day, tomorrow)
            ? t.tomorrowLabel
            : DateFormat('EEEE, d MMM', locale).format(day);

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
                // Upcoming jobs are grouped under the day they're due.
                if (state.filter == TasksFilter.upcoming &&
                    (i == 0 || !DateUtils.isSameDay(tasks[i - 1].scheduledAt, tasks[i].scheduledAt))) ...[
                  if (i > 0) const SizedBox(height: 20),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 4),
                    child: Text(dayLabel(tasks[i].scheduledAt!), style: AppTextStyles.sectionTitle),
                  ),
                  const SizedBox(height: 12),
                ] else if (i > 0)
                  const SizedBox(height: 12),
                KeyedSubtree(
                  key: _keyFor(tasks[i].id),
                  child: InspectorTaskCard(
                    task: tasks[i],
                    showDate: state.filter == TasksFilter.overdue,
                    overdueDays: InspectorTasksState.daysOverdue(tasks[i]),
                    onContinueCertificate: () => _continueCertificate(tasks[i]),
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
