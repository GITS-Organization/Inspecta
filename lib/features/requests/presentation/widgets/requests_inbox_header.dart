import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/shared/m_search_field.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../bloc/requests_list_cubit.dart';
import 'requests_tab_bar.dart';

/// The inbox's header: avatar, role + greeting, bell, the "Requests" title,
/// and the search box with the New / Maybe toggle beside it.
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
      bottom: Row(
        children: [
          Expanded(
            child: ISearchField(
              controller: searchController,
              onChanged: context.read<RequestsListCubit>().setQuery,
              hintText: t.searchRequestsHint,
            ),
          ),
          const SizedBox(width: 8),
          RequestsTabBar(
            selected: context.select((RequestsListCubit c) => c.state.tab),
            onChanged: context.read<RequestsListCubit>().setTab,
          ),
        ],
      ),
    );
  }
}
