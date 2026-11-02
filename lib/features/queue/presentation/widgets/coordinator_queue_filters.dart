import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/app_segmented_filter.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/queue_filter.dart';
import '../bloc/coordinator_queue_cubit.dart';

/// The queue's filter row — Unassigned / Scheduled / In progress / To send /
/// Delivered, each with its live job count. Tapping one narrows the list to
/// that group; tapping it again clears the filter (the cubit's toggle).
/// Unassigned is red when there's anything waiting on the coordinator.
///
/// Five groups don't fit side by side, so the control scrolls sideways — the
/// one cut off at the edge says there is more.
class CoordinatorQueueFilters extends StatelessWidget {
  const CoordinatorQueueFilters({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return BlocBuilder<CoordinatorQueueCubit, CoordinatorQueueState>(
      builder: (context, state) {
        AppSegment<QueueFilter> segment(QueueFilter filter, String label) => AppSegment(
          value: filter,
          label: label,
          count: state.countOf(filter),
          alert: filter == QueueFilter.unassigned,
        );

        // Sits inside the header banner, which already has its own gutter.
        return AppSegmentedFilter<QueueFilter>(
          margin: EdgeInsets.zero,
          segments: [
            segment(QueueFilter.unassigned, t.statUnassigned),
            segment(QueueFilter.scheduled, t.statScheduled),
            segment(QueueFilter.inProgress, t.statInProgress),
            segment(QueueFilter.toSend, t.statToSend),
            segment(QueueFilter.delivered, t.statDelivered),
          ],
          selected: state.filter,
          onChanged: context.read<CoordinatorQueueCubit>().toggleFilter,
        );
      },
    );
  }
}
