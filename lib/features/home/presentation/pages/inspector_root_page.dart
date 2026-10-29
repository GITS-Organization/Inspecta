import 'package:flutter/material.dart';

import '../../../auth/presentation/pages/profile_page.dart';
import '../../../inspector/presentation/pages/inspector_certificates_page.dart';
import '../../../inspector/presentation/pages/inspector_tasks_page.dart';
import '../../../inspector/presentation/widgets/inspector_bottom_nav.dart';
import '../../../timesheet/presentation/pages/inspector_timesheet_page.dart';

/// The inspector's shell: Tasks, Certificates and Timesheet are real tabs —
/// same in-place-swap shape as `CoordinatorRootPage`.
class InspectorRootPage extends StatefulWidget {
  const InspectorRootPage({super.key});

  @override
  State<InspectorRootPage> createState() => _InspectorRootPageState();
}

class _InspectorRootPageState extends State<InspectorRootPage> {
  InspectorTab _tab = InspectorTab.tasks;

  /// A screen is only built the first time it's opened, so each one makes its
  /// own requests then rather than all of them at launch; once built it stays
  /// alive.
  final _opened = {InspectorTab.tasks};

  void _select(InspectorTab tab) => setState(() {
    _tab = tab;
    _opened.add(tab);
  });

  void _openProfile() => _select(InspectorTab.profile);

  Widget _page(InspectorTab tab) => switch (tab) {
    InspectorTab.tasks => InspectorTasksPage(onOpenProfile: _openProfile),
    InspectorTab.certificates => InspectorCertificatesPage(onOpenProfile: _openProfile),
    InspectorTab.timesheet => InspectorTimesheetPage(onOpenProfile: _openProfile),
    InspectorTab.profile => const ProfilePage(),
  };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _tab.index,
        children: [
          for (final tab in InspectorTab.values)
            _opened.contains(tab) ? _page(tab) : const SizedBox.shrink(),
        ],
      ),
      bottomNavigationBar: InspectorBottomNav(selected: _tab, onSelectTab: _select),
    );
  }
}
