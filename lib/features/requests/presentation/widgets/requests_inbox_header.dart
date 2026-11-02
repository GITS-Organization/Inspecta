import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/shared/m_search_field.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../bloc/requests_list_cubit.dart';
import 'requests_tab_bar.dart';

/// The inbox's header: avatar, role + greeting, bell, the "Requests" title,
/// the search box and, under it, the New / Maybe toggle.
class RequestsInboxHeader extends StatelessWidget {
  final TextEditingController searchController;
  final VoidCallback onOpenProfile;

  const RequestsInboxHeader({
    super.key,
    required this.searchController,
    required this.onOpenProfile,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return UserAppHeader(
      title: t.navRequests,
      onOpenProfile: onOpenProfile,
      bottom: Column(
        children: [
          ISearchField(
            controller: searchController,
            onChanged: context.read<RequestsListCubit>().setQuery,
            hintText: t.searchRequestsHint,
          ),
          const SizedBox(height: 12),
          RequestsTabBar(
            selected: context.select((RequestsListCubit c) => c.state.tab),
            newCount: context.select((RequestsListCubit c) => c.state.newTab.requests.items.length),
            onChanged: context.read<RequestsListCubit>().setTab,
          ),
        ],
      ),
    );
  }
}
