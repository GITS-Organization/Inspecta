import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/app_segmented_filter.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/inspector_tasks_cubit.dart';

/// The Tasks tab's filter row — Today / Upcoming / Overdue, each with its
/// live count. They double as the tabs choosing which list shows, and
/// Overdue's count turns red whenever something's late (Feature 05).
class InspectorTasksFilters extends StatelessWidget {
  const InspectorTasksFilters({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<InspectorTasksCubit, InspectorTasksState>(
      builder: (context, state) {
        AppSegment<TasksFilter> segment(TasksFilter filter, String label) => AppSegment(
          value: filter,
          label: label,
          count: state.countFor(filter),
          alert: filter == TasksFilter.overdue,
        );

        return AppSegmentedFilter<TasksFilter>(
          segments: [
            segment(TasksFilter.today, t.tasksFilterToday),
            segment(TasksFilter.upcoming, t.tasksFilterUpcoming),
            segment(TasksFilter.overdue, t.tasksFilterOverdue),
          ],
          selected: state.filter,
          onChanged: context.read<InspectorTasksCubit>().selectFilter,
        );
      },
    );
  }
}
