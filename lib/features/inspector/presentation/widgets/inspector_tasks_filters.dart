import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/app_filter_chip.dart';
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
        final cubit = context.read<InspectorTasksCubit>();
        Widget chip(TasksFilter filter, String label) => AppFilterChip(
          label: label,
          count: state.countFor(filter),
          alert: filter == TasksFilter.overdue,
          selected: state.filter == filter,
          onTap: () => cubit.selectFilter(filter),
        );

        return SizedBox(
          height: 40,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            children: [
              chip(TasksFilter.today, t.tasksFilterToday),
              const SizedBox(width: 8),
              chip(TasksFilter.upcoming, t.tasksFilterUpcoming),
              const SizedBox(width: 8),
              chip(TasksFilter.overdue, t.tasksFilterOverdue),
            ],
          ),
        );
      },
    );
  }
}
