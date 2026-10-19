import 'package:flutter/material.dart';

import '../../../auth/presentation/pages/profile_page.dart';
import '../../../clients/presentation/pages/clients_list_page.dart';
import '../../../coordinator/presentation/pages/coordinator_more_page.dart';
import '../../../coordinator/presentation/pages/coordinator_queue_page.dart';
import '../../../coordinator/presentation/pages/coordinator_schedule_page.dart';
import '../../../coordinator/presentation/pages/inspectors_list_page.dart';
import '../../../coordinator/presentation/widgets/coordinator_bottom_nav.dart';
import '../../../quotation/presentation/pages/quotations_list_page.dart';
import '../../../requests/presentation/pages/requests_inbox_page.dart';
import '../../../timesheet/presentation/pages/coordinator_timesheets_page.dart';

/// The coordinator's shell — the one role that runs a job from the inbox to
/// the inspector: Requests, Quotations, Queue and Schedule in the bottom bar,
/// with Clients, Inspectors, Timesheets and Profile behind More. Every screen
/// swaps in place in the same Scaffold, with the bar staying put and tracking
/// which one's active — none of them is a separate pushed screen.
class CoordinatorRootPage extends StatefulWidget {
  const CoordinatorRootPage({super.key});

  @override
  State<CoordinatorRootPage> createState() => _CoordinatorRootPageState();
}

class _CoordinatorRootPageState extends State<CoordinatorRootPage> {
  CoordinatorTab _tab = CoordinatorTab.requests;

  /// A screen is only built the first time it's opened, so the nine of them
  /// don't all subscribe to Firestore up front; once built it stays alive.
  final _opened = {CoordinatorTab.requests};

  void _select(CoordinatorTab tab) => setState(() {
    _tab = tab;
    _opened.add(tab);
  });

  Widget _page(CoordinatorTab tab) => switch (tab) {
    CoordinatorTab.requests => RequestsInboxPage(onOpenProfile: () => _select(CoordinatorTab.profile)),
    CoordinatorTab.quotations => const QuotationsListPage(),
    CoordinatorTab.queue => CoordinatorQueuePage(onOpenProfile: () => _select(CoordinatorTab.profile)),
    CoordinatorTab.schedule => const CoordinatorSchedulePage(),
    CoordinatorTab.more => CoordinatorMorePage(onSelect: _select),
    CoordinatorTab.clients => const ClientsListPage(),
    CoordinatorTab.inspectors => const InspectorsListPage(),
    CoordinatorTab.timesheets => const CoordinatorTimesheetsPage(),
    CoordinatorTab.profile => const ProfilePage(),
  };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      // IndexedStack keeps every opened tab's state (scroll position, the
      // streams already subscribed) alive when switching, instead of
      // rebuilding from scratch each time.
      body: IndexedStack(
        index: _tab.index,
        children: [
          for (final tab in CoordinatorTab.values)
            _opened.contains(tab) ? _page(tab) : const SizedBox.shrink(),
        ],
      ),
      bottomNavigationBar: CoordinatorBottomNav(selected: _tab, onSelectTab: _select),
    );
  }
}
