import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../auth/presentation/widgets/auth_labels.dart';
import '../../domain/queue_filter.dart';
import '../bloc/coordinator_queue_cubit.dart';

/// The queue's blue header: avatar, role/screen title, notifications, and
/// the four job-count tiles ("Unassigned / Scheduled / In progress / To send"),
/// each of which filters the list below when tapped.
class CoordinatorQueueHeader extends StatelessWidget {
  final VoidCallback onOpenProfile;

  const CoordinatorQueueHeader({super.key, required this.onOpenProfile});

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
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Material(
                color: Colors.white,
                shape: const CircleBorder(),
                child: InkWell(
                  customBorder: const CircleBorder(),
                  onTap: onOpenProfile,
                  child: SizedBox.square(
                    dimension: 44,
                    child: Center(
                      child: Text(
                        user.initials,
                        style: const TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w700,
                          color: AppColours.primaryDark,
                        ),
                      ),
                    ),
                  ),
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
          BlocBuilder<CoordinatorQueueCubit, CoordinatorQueueState>(
            builder: (context, state) {
              final cubit = context.read<CoordinatorQueueCubit>();
              Widget tile(QueueFilter filter, String label) => Expanded(
                child: _FilterTile(
                  value: state.countOf(filter),
                  label: label,
                  selected: state.filter == filter,
                  onTap: () => cubit.toggleFilter(filter),
                ),
              );
              return Row(
                children: [
                  tile(QueueFilter.unassigned, t.statUnassigned),
                  const SizedBox(width: 8),
                  tile(QueueFilter.scheduled, t.statScheduled),
                  const SizedBox(width: 8),
                  tile(QueueFilter.inProgress, t.statInProgress),
                  const SizedBox(width: 8),
                  tile(QueueFilter.toSend, t.statToSend),
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}

/// A count tile that doubles as a filter: white while its group is the one
/// the list is narrowed to, translucent otherwise. Tapping it again clears
/// the filter.
class _FilterTile extends StatelessWidget {
  final int value;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _FilterTile({
    required this.value,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      selected: selected,
      child: Material(
        color: selected ? Colors.white : Colors.white.withValues(alpha: 0.16),
        borderRadius: BorderRadius.circular(14),
        child: InkWell(
          borderRadius: BorderRadius.circular(14),
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(10),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  '$value',
                  style: TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    color: selected ? AppColours.chipAmberTextStrong : Colors.white,
                  ),
                ),
                Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.caption.copyWith(
                    fontWeight: FontWeight.w600,
                    color: selected ? AppColours.inkSecondary : AppColours.onPrimaryMuted,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
