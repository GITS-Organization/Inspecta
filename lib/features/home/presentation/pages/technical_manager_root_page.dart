import 'package:flutter/material.dart';

import '../../../auth/presentation/pages/profile_page.dart';
import '../../../technical_manager/presentation/pages/review_queue_page.dart';
import '../../../technical_manager/presentation/pages/sent_certificates_page.dart';
import '../../../technical_manager/presentation/widgets/review_bottom_nav.dart';

/// The technical manager's shell: Review, Sent and Profile — same
/// in-place-swap shape as `CoordinatorRootPage`.
class TechnicalManagerRootPage extends StatefulWidget {
  const TechnicalManagerRootPage({super.key});

  @override
  State<TechnicalManagerRootPage> createState() => _TechnicalManagerRootPageState();
}

class _TechnicalManagerRootPageState extends State<TechnicalManagerRootPage> {
  TechnicalManagerTab _tab = TechnicalManagerTab.review;

  /// A screen is only built the first time it's opened, so each one makes its
  /// own requests then rather than all of them at launch; once built it stays
  /// alive.
  final _opened = {TechnicalManagerTab.review};

  void _select(TechnicalManagerTab tab) => setState(() {
    _tab = tab;
    _opened.add(tab);
  });

  void _openProfile() => _select(TechnicalManagerTab.profile);

  Widget _page(TechnicalManagerTab tab) => switch (tab) {
    TechnicalManagerTab.review => ReviewQueuePage(onOpenProfile: _openProfile),
    TechnicalManagerTab.sent => SentCertificatesPage(onOpenProfile: _openProfile),
    TechnicalManagerTab.profile => const ProfilePage(),
  };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _tab.index,
        children: [
          for (final tab in TechnicalManagerTab.values)
            _opened.contains(tab) ? _page(tab) : const SizedBox.shrink(),
        ],
      ),
      bottomNavigationBar: ReviewBottomNav(selected: _tab, onSelectTab: _select),
    );
  }
}
