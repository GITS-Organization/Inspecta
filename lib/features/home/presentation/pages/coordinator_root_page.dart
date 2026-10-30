import 'package:flutter/material.dart';

import '../../../auth/presentation/pages/profile_page.dart';
import '../../../queue/presentation/pages/coordinator_queue_page.dart';
import '../../../coordinator/presentation/widgets/coordinator_bottom_nav.dart';
import '../../../quotation/presentation/pages/quotations_list_page.dart';
import '../../../requests/presentation/pages/requests_inbox_page.dart';

class CoordinatorRootPage extends StatefulWidget {
  const CoordinatorRootPage({super.key});

  @override
  State<CoordinatorRootPage> createState() => _CoordinatorRootPageState();
}

class _CoordinatorRootPageState extends State<CoordinatorRootPage> {
  CoordinatorTab _tab = CoordinatorTab.requests;

  final _opened = {CoordinatorTab.requests};

  void _select(CoordinatorTab tab) => setState(() {
    _tab = tab;
    _opened.add(tab);
  });

  Widget _page(CoordinatorTab tab) => switch (tab) {
    CoordinatorTab.requests => RequestsInboxPage(
      onOpenProfile: () => _select(CoordinatorTab.more),
    ),
    CoordinatorTab.quotations => QuotationsListPage(
      onOpenProfile: () => _select(CoordinatorTab.more),
    ),
    CoordinatorTab.queue => CoordinatorQueuePage(
      onOpenProfile: () => _select(CoordinatorTab.more),
    ),
    CoordinatorTab.more => const ProfilePage(),
  };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _tab.index,
        children: [
          for (final tab in CoordinatorTab.values)
            _opened.contains(tab) ? _page(tab) : const SizedBox.shrink(),
        ],
      ),
      bottomNavigationBar: CoordinatorBottomNav(
        selected: _tab,
        onSelectTab: _select,
      ),
    );
  }
}
