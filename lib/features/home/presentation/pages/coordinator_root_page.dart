import 'package:flutter/material.dart';

import '../../../auth/presentation/pages/profile_page.dart';
import '../../../coordinator/presentation/pages/coordinator_queue_page.dart';
import '../../../coordinator/presentation/widgets/coordinator_bottom_nav.dart';
import '../../../quotation/presentation/pages/quotations_list_page.dart';
import '../../../requests/presentation/pages/requests_inbox_page.dart';

/// The coordinator's shell — the one role that runs a job from the inbox to
/// the inspector and, once the technical manager has approved its
/// certificate, on to the client: Requests, Quotations and Queue in the
/// bottom bar, then More, which is the user's Profile. Clients, inspectors
/// and timesheets are managed from the dashboard, not here. Every screen
/// swaps in place in the same Scaffold, with the bar staying put and
/// tracking which one's active — none of them is a separate pushed screen.
class CoordinatorRootPage extends StatefulWidget {
  const CoordinatorRootPage({super.key});

  @override
  State<CoordinatorRootPage> createState() => _CoordinatorRootPageState();
}

class _CoordinatorRootPageState extends State<CoordinatorRootPage> {
  CoordinatorTab _tab = CoordinatorTab.requests;

  /// A screen is only built the first time it's opened, so the four of them
  /// don't all subscribe to Firestore up front; once built it stays alive.
  final _opened = {CoordinatorTab.requests};

  void _select(CoordinatorTab tab) => setState(() {
    _tab = tab;
    _opened.add(tab);
  });

  Widget _page(CoordinatorTab tab) => switch (tab) {
    CoordinatorTab.requests => RequestsInboxPage(onOpenProfile: () => _select(CoordinatorTab.more)),
    CoordinatorTab.quotations => const QuotationsListPage(),
    CoordinatorTab.queue => CoordinatorQueuePage(onOpenProfile: () => _select(CoordinatorTab.more)),
    CoordinatorTab.more => const ProfilePage(),
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
