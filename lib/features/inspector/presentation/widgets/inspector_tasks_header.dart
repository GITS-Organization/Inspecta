import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../auth/presentation/widgets/auth_labels.dart';
import '../bloc/inspector_tasks_cubit.dart';

/// The Tasks tab's blue header: avatar, the screen title, a (visual-only)
/// notifications bell, and three tiles — Today, Upcoming and Overdue, each
/// with its count — that double as the tabs choosing which list shows
/// (Feature 05).
class InspectorTasksHeader extends StatelessWidget {
  const InspectorTasksHeader({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final user = context.select((AuthCubit cubit) => cubit.user);
    if (user == null) return const SizedBox.shrink();

    return Container(
      padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 24, 20, 22),
      decoration: const BoxDecoration(
        color: AppColours.primaryColor,
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Container(
                width: 44,
                height: 44,
                alignment: Alignment.center,
                decoration: const BoxDecoration(color: Colors.white, shape: BoxShape.circle),
                child: Text(
                  user.initials,
                  style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: AppColours.primaryDark),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      user.role.label(t),
                      style: AppTextStyles.caption.copyWith(color: AppColours.onPrimaryMuted),
                    ),
                    Text(
                      user.role.homeTitle(t),
                      overflow: TextOverflow.ellipsis,
                      style: AppTextStyles.emphasis.copyWith(color: Colors.white, fontWeight: FontWeight.w700),
                    ),
                  ],
                ),
              ),
              IconButton(
                onPressed: null,
                tooltip: t.notifications,
                style: IconButton.styleFrom(
                  fixedSize: const Size.square(44),
                  backgroundColor: Colors.white.withValues(alpha: 0.16),
                  disabledBackgroundColor: Colors.white.withValues(alpha: 0.16),
                ),
                icon: const Icon(Icons.notifications_none_rounded, color: Colors.white),
              ),
            ],
          ),
          const SizedBox(height: 18),
          const _FilterTiles(),
        ],
      ),
    );
  }
}

class _FilterTiles extends StatelessWidget {
  const _FilterTiles();

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<InspectorTasksCubit, InspectorTasksState>(
      builder: (context, state) {
        final cubit = context.read<InspectorTasksCubit>();
        Widget tile(TasksFilter filter, String label) => Expanded(
          child: _FilterTile(
            label: label,
            count: state.countFor(filter),
            selected: state.filter == filter,
            alert: filter == TasksFilter.overdue,
            onTap: () => cubit.selectFilter(filter),
          ),
        );
        return Row(
          children: [
            tile(TasksFilter.today, t.tasksFilterToday),
            const SizedBox(width: 8),
            tile(TasksFilter.upcoming, t.tasksFilterUpcoming),
            const SizedBox(width: 8),
            tile(TasksFilter.overdue, t.tasksFilterOverdue),
          ],
        );
      },
    );
  }
}

/// One of the three tiles. The selected one is white; Overdue turns red,
/// unselected, whenever there's something overdue, so it can't be missed.
class _FilterTile extends StatelessWidget {
  final String label;
  final int count;
  final bool selected;
  final bool alert;
  final VoidCallback onTap;

  const _FilterTile({
    required this.label,
    required this.count,
    required this.selected,
    required this.alert,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final alerting = alert && count > 0;
    final background = selected
        ? Colors.white
        : (alerting ? AppColours.errorIcon : Colors.white.withValues(alpha: 0.16));
    final numberColour = selected ? (alerting ? AppColours.errorIcon : AppColours.primaryDark) : Colors.white;
    final labelColour = selected ? AppColours.inkSecondary : (alerting ? Colors.white : AppColours.onPrimaryMuted);
    return Semantics(
      button: true,
      selected: selected,
      label: '$label $count',
      child: Material(
        color: background,
        borderRadius: BorderRadius.circular(14),
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: onTap,
          child: Container(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('$count', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800, color: numberColour)),
                Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w600, color: labelColour),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
