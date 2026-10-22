import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../../../certificate/presentation/pages/certificate_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/inspector_tasks_cubit.dart';
import '../widgets/decline_task_dialog.dart';
import '../widgets/inspector_task_card.dart';
import '../widgets/inspector_tasks_filters.dart';
import '../widgets/new_task_banner.dart';

/// Feature 05's Tasks tab: Today, Upcoming and Overdue lists (chosen from the
/// header's tiles), a nudge toward any job the inspector hasn't responded to
/// yet, and the task cards.
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

  Future<void> _decline(InspectionRequest task) async {
    final cubit = context.read<InspectorTasksCubit>();
    final reason = await showDeclineTaskDialog(context);
    if (reason == null) return;
    await cubit.decline(task.id, reason: reason.isEmpty ? null : reason);
  }

  void _continueCertificate(InspectionRequest task) {
    if (task.status == JobStatus.taskAccepted) {
      context.read<InspectorTasksCubit>().startInspection(task.id);
    }
    Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => CertificatePage(request: task)),
    );
  }

  /// Takes the inspector to the job that needs a response: switching to the
  /// list it's in first, if it isn't the one showing, then scrolling to it.
  void _openFirstNeedsResponse(InspectorTasksState state) {
    final task = state.firstNeedsResponse;
    if (task == null) return;
    final filter = InspectorTasksState.filterOf(task);
    if (filter != null && filter != state.filter) context.read<InspectorTasksCubit>().selectFilter(filter);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final targetContext = _cardKeys[task.id]?.currentContext;
      if (targetContext == null || !targetContext.mounted) return;
      Scrollable.ensureVisible(targetContext, duration: const Duration(milliseconds: 300), curve: Curves.easeInOut);
    });
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocConsumer<InspectorTasksCubit, InspectorTasksState>(
      listenWhen: (previous, current) => current.actionSeq != previous.actionSeq,
      listener: (context, state) {
        if (state.lastActionSuccess) {
          final message = switch (state.lastAction) {
            InspectorTaskAction.accept => t.taskAcceptedMessage,
            InspectorTaskAction.decline => t.taskDeclinedMessage,
            _ => null,
          };
          if (message != null) MToast.showSuccess(message: message);
        } else if (state.lastActionFailure != null) {
          MToast.showError(message: state.lastActionFailure!.message(t));
        }
      },
      builder: (context, state) {
        if (state.status == InspectorTasksStatus.loading) {
          return Loading.loader(context);
        }
        if (state.status == InspectorTasksStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final tasks = state.visibleTasks;
        final banner = state.firstNeedsResponse;
        final cubit = context.read<InspectorTasksCubit>();
        final locale = Localizations.localeOf(context).languageCode;
        final tomorrow = DateTime.now().add(const Duration(days: 1));

        String dayLabel(DateTime day) => DateUtils.isSameDay(day, tomorrow)
            ? t.tomorrowLabel
            : DateFormat('EEEE, d MMM', locale).format(day);

        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, AppLayout.bottomNavClearance),
          children: [
            if (banner != null) ...[
              NewTaskBanner(onOpen: () => _openFirstNeedsResponse(state)),
              const SizedBox(height: 12),
            ],
            if (tasks.isEmpty)
              _EmptyState(filter: state.filter)
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
                    submitting: state.isSubmitting,
                    onAccept: () => cubit.accept(tasks[i].id),
                    onDecline: () => _decline(tasks[i]),
                    onContinueCertificate: () => _continueCertificate(tasks[i]),
                    onNavigate: () => _openInMaps(tasks[i]),
                  ),
                ),
              ],
          ],
        );
      },
    );
  }
}

class _EmptyState extends StatelessWidget {
  final TasksFilter filter;

  const _EmptyState({required this.filter});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final (icon, message) = switch (filter) {
      TasksFilter.today => (Icons.event_available_outlined, t.emptyTasksToday),
      TasksFilter.upcoming => (Icons.event_note_outlined, t.emptyTasksUpcoming),
      TasksFilter.overdue => (Icons.check_circle_outline_rounded, t.emptyTasksOverdue),
    };
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 48, horizontal: 16),
      child: Column(
        children: [
          Icon(icon, size: 40, color: AppColours.inkMuted),
          const SizedBox(height: 12),
          Text(message, textAlign: TextAlign.center, style: AppTextStyles.subtitle),
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
