import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/consts/app_colors.dart';
import 'package:inspecta/features/coordinator/presentation/widgets/coordinator_bottom_nav.dart';
import 'package:inspecta/features/coordinator/presentation/widgets/coordinator_more_section.dart';
import 'package:inspecta/l10n/app_localizations.dart';

Future<void> _pump(WidgetTester tester, Widget child) async {
  tester.view.physicalSize = const Size(390, 844) * 3;
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);

  await tester.pumpWidget(
    MaterialApp(
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: child,
    ),
  );
}

Color? _labelColour(WidgetTester tester, String label) =>
    tester.widget<Text>(find.text(label)).style?.color;

void main() {
  test('only the first three screens and More have a slot in the bar', () {
    expect(
      CoordinatorTab.values.where((tab) => tab.inBar),
      [
        CoordinatorTab.requests,
        CoordinatorTab.quotations,
        CoordinatorTab.queue,
        CoordinatorTab.more,
      ],
    );
  });

  group('CoordinatorBottomNav', () {
    Future<void> pumpNav(
      WidgetTester tester, {
      required CoordinatorTab selected,
      ValueChanged<CoordinatorTab>? onSelect,
    }) => _pump(
      tester,
      Scaffold(
        bottomNavigationBar: CoordinatorBottomNav(selected: selected, onSelectTab: onSelect ?? (_) {}),
      ),
    );

    testWidgets('shows Requests, Quotations, Queue and More', (tester) async {
      await pumpNav(tester, selected: CoordinatorTab.requests);

      for (final label in ['Requests', 'Quotations', 'Queue', 'More']) {
        expect(find.text(label), findsOneWidget);
      }
      // Those live behind More, not in the bar — and Schedule is gone: the
      // Inspectors screen already shows each inspector's day.
      for (final label in ['Schedule', 'Clients', 'Inspectors', 'Timesheets']) {
        expect(find.text(label), findsNothing);
      }
    });

    testWidgets('highlights the selected tab', (tester) async {
      await pumpNav(tester, selected: CoordinatorTab.queue);

      expect(_labelColour(tester, 'Queue'), AppColours.primaryDark);
      expect(_labelColour(tester, 'Requests'), AppColours.inkMuted);
      expect(_labelColour(tester, 'More'), AppColours.inkMuted);
    });

    testWidgets('keeps More highlighted while a screen behind it shows', (tester) async {
      for (final tab in [
        CoordinatorTab.clients,
        CoordinatorTab.inspectors,
        CoordinatorTab.timesheets,
      ]) {
        await pumpNav(tester, selected: tab);

        expect(_labelColour(tester, 'More'), AppColours.primaryDark, reason: '$tab');
        expect(_labelColour(tester, 'Requests'), AppColours.inkMuted, reason: '$tab');
      }
    });

    testWidgets('reports the tab that was tapped', (tester) async {
      final tapped = <CoordinatorTab>[];
      await pumpNav(tester, selected: CoordinatorTab.requests, onSelect: tapped.add);

      for (final label in ['Quotations', 'Queue', 'More', 'Requests']) {
        await tester.tap(find.text(label));
      }

      expect(tapped, [
        CoordinatorTab.quotations,
        CoordinatorTab.queue,
        CoordinatorTab.more,
        CoordinatorTab.requests,
      ]);
    });
  });

  group('CoordinatorMoreSection', () {
    testWidgets('opens Clients, Inspectors and Timesheets', (tester) async {
      final picked = <CoordinatorTab>[];
      await _pump(tester, Scaffold(body: CoordinatorMoreSection(onSelect: picked.add)));

      for (final label in ['Clients', 'Inspectors', 'Timesheets']) {
        await tester.tap(find.text(label));
      }

      expect(picked, [
        CoordinatorTab.clients,
        CoordinatorTab.inspectors,
        CoordinatorTab.timesheets,
      ]);
    });
  });
}
