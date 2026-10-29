import 'package:flutter/material.dart';

import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';

/// What a list shows when it has nothing to show: a search that matched
/// nothing, filters that left nothing, or no rows at all. Texts are passed in
/// already localized.
class ListEmpty extends StatelessWidget {
  final bool hasSearch;
  final bool hasFilters;

  /// Title when the list is empty with no search or filter applied.
  final String noneTitle;
  final String? noneMessage;

  /// Title when filters (but no search) left nothing.
  final String noMatchTitle;
  final String? noMatchMessage;

  /// Message when a search matched nothing.
  final String searchMessage;

  final String? clearSearchLabel;
  final String? clearFiltersLabel;
  final VoidCallback? onClearSearch;
  final VoidCallback? onClearFilters;

  /// A smaller version for use inside a card or a short panel.
  final bool compact;

  const ListEmpty({
    super.key,
    required this.hasSearch,
    required this.hasFilters,
    required this.noneTitle,
    required this.noMatchTitle,
    required this.searchMessage,
    this.noneMessage,
    this.noMatchMessage,
    this.clearSearchLabel,
    this.clearFiltersLabel,
    this.onClearSearch,
    this.onClearFilters,
    this.compact = false,
  });

  @override
  Widget build(BuildContext context) {
    if (hasSearch) {
      return EmptyState(
        icon: Icons.search_off_rounded,
        title: noMatchTitle,
        message: searchMessage,
        actionLabel: onClearSearch == null ? null : clearSearchLabel,
        onAction: onClearSearch,
        compact: compact,
      );
    }

    if (hasFilters) {
      return EmptyState(
        icon: Icons.filter_list_off_rounded,
        title: noMatchTitle,
        message: noMatchMessage,
        actionLabel: onClearFilters == null ? null : clearFiltersLabel,
        onAction: onClearFilters,
        compact: compact,
      );
    }

    return EmptyState(
      icon: Icons.inbox_outlined,
      title: noneTitle,
      message: noneMessage,
      compact: compact,
    );
  }
}

/// An icon, a title, an optional message and an optional action, centred.
class EmptyState extends StatelessWidget {
  final IconData icon;
  final String title;
  final String? message;
  final String? actionLabel;
  final VoidCallback? onAction;
  final bool compact;

  const EmptyState({
    super.key,
    required this.icon,
    required this.title,
    this.message,
    this.actionLabel,
    this.onAction,
    this.compact = false,
  });

  @override
  Widget build(BuildContext context) {
    final message = this.message;
    final actionLabel = this.actionLabel;

    return Center(
      child: Padding(
        padding: EdgeInsets.symmetric(
          horizontal: 24,
          vertical: compact ? 16 : 32,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: compact ? 36 : 56, color: AppColours.inkMuted),
            SizedBox(height: compact ? 8 : 16),
            Text(
              title,
              textAlign: TextAlign.center,
              style: AppTextStyles.cardTitle,
            ),
            if (message != null && message.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                message,
                textAlign: TextAlign.center,
                style: AppTextStyles.subtitle,
              ),
            ],
            if (actionLabel != null && onAction != null) ...[
              SizedBox(height: compact ? 12 : 20),
              OutlinedButton(onPressed: onAction, child: Text(actionLabel)),
            ],
          ],
        ),
      ),
    );
  }
}
