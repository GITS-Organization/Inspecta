import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_search_field.dart';
import '../../../../l10n/app_localizations.dart';

/// The Clients tab's header: title + "Add client" on one row, the search
/// box below, both inside one continuous rounded blue panel (matching
/// the Quotations tab's header).
class ClientsListHeader extends StatelessWidget {
  final TextEditingController searchController;
  final ValueChanged<String> onSearchChanged;
  final VoidCallback onAddClient;

  const ClientsListHeader({
    super.key,
    required this.searchController,
    required this.onSearchChanged,
    required this.onAddClient,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Container(
      padding: EdgeInsets.fromLTRB(20, MediaQuery.paddingOf(context).top + 22, 20, 20),
      decoration: const BoxDecoration(
        color: AppColours.primaryColor,
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(28)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      t.roleCoordinator,
                      style: AppTextStyles.caption.copyWith(color: AppColours.onPrimaryMuted),
                    ),
                    Text(
                      t.clientsTitle,
                      style: AppTextStyles.pageTitle.copyWith(color: Colors.white, fontSize: 22),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              SizedBox(
                height: 44,
                child: ElevatedButton.icon(
                  onPressed: onAddClient,
                  icon: const Icon(Icons.add_rounded, size: 18),
                  label: Text(t.addClient),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: Colors.white,
                    foregroundColor: AppColours.primaryDark,
                    elevation: 0,
                    padding: const EdgeInsets.symmetric(horizontal: 14),
                    textStyle: AppTextStyles.badge.copyWith(fontSize: 14),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          ISearchField(
            controller: searchController,
            onChanged: onSearchChanged,
            hintText: t.searchClientsHint,
          ),
        ],
      ),
    );
  }
}
