import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/app_filter_chip.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/queue_filter.dart';
import '../bloc/coordinator_queue_cubit.dart';

/// The queue's filter row — Unassigned / Scheduled / In progress / To send,
/// each with its live job count. Tapping one narrows the list to that group;
/// tapping it again clears the filter. Unassigned is red when there's
/// anything waiting on the coordinator.
class CoordinatorQueueFilters extends StatelessWidget {
  const CoordinatorQueueFilters({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocBuilder<CoordinatorQueueCubit, CoordinatorQueueState>(
      builder: (context, state) {
        final cubit = context.read<CoordinatorQueueCubit>();
        Widget chip(QueueFilter filter, String label) => AppFilterChip(
          label: label,
          count: state.countOf(filter),
          alert: filter == QueueFilter.unassigned,
          selected: state.filter == filter,
          onTap: () => cubit.toggleFilter(filter),
        );

        return SizedBox(
          height: 40,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            children: [
              chip(QueueFilter.unassigned, t.statUnassigned),
              const SizedBox(width: 8),
              chip(QueueFilter.scheduled, t.statScheduled),
              const SizedBox(width: 8),
              chip(QueueFilter.inProgress, t.statInProgress),
              const SizedBox(width: 8),
              chip(QueueFilter.toSend, t.statToSend),
              const SizedBox(width: 8),
              chip(QueueFilter.delivered, t.statDelivered),
            ],
          ),
        );
      },
    );
  }
}
