import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/core/widgets/app_bottom_nav.dart';
import 'package:inspecta/features/requests/domain/entities/inspection_request.dart';
import 'package:inspecta/features/requests/domain/entities/request_source.dart';
import 'package:inspecta/features/requests/presentation/widgets/job_stepper.dart';
import 'package:inspecta/features/requests/presentation/widgets/request_card.dart';
import 'package:inspecta/l10n/app_localizations.dart';

Widget _app(Widget child) => MaterialApp(
  localizationsDelegates: const [
    AppLocalizations.delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
  ],
  supportedLocales: AppLocalizations.supportedLocales,
  home: Scaffold(body: SingleChildScrollView(child: child)),
);

void main() {
  setUp(() {});

  testWidgets('the bottom nav shows every label', (tester) async {
    tester.view.physicalSize = const Size(375 * 3, 812 * 3);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    var selected = 0;
    await tester.pumpWidget(
      _app(
        StatefulBuilder(
          builder: (context, setState) => AppBottomNav(
            selectedIndex: selected,
            onSelect: (i) => setState(() => selected = i),
            items: const [
              AppNavItem(icon: Icons.inbox_rounded, label: 'Requests'),
              AppNavItem(icon: Icons.request_quote_rounded, label: 'Quotes'),
              AppNavItem(icon: Icons.pending_actions_rounded, label: 'Queue'),
              AppNavItem(icon: Icons.more_horiz_rounded, label: 'More'),
            ],
          ),
        ),
      ),
    );
    for (final label in ['Requests', 'Quotes', 'Queue', 'More']) {
      expect(find.text(label), findsOneWidget);
    }
    await tester.tap(find.text('Queue'));
    await tester.pumpAndSettle();
    expect(selected, 2);
    expect(tester.takeException(), isNull);
  });

  testWidgets('the stepper marks finished steps and the chosen step', (tester) async {
    tester.view.physicalSize = const Size(375 * 3, 812 * 3);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      _app(const Padding(padding: EdgeInsets.all(20), child: JobStepper(status: JobStatus.assigned, currentStep: 2))),
    );
    expect(find.byIcon(Icons.check_rounded), findsNWidgets(2));
    expect(tester.takeException(), isNull);
  });

  testWidgets('a request card keeps its status chips under the title', (tester) async {
    tester.view.physicalSize = const Size(375 * 3, 812 * 3);
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    final request = InspectionRequest(
      id: 'abc',
      source: RequestSource.email,
      clientName: 'ElRagab Heavy steal',
      receivedAt: DateTime.now().subtract(const Duration(days: 2)),
      location: 'smouha',
      status: JobStatus.requestReceived,
      items: const [],
      subject: '3ayzk f el masn3',
    );
    await tester.pumpWidget(
      _app(Padding(padding: const EdgeInsets.all(16), child: RequestCard(request: request, onTap: () {}))),
    );
    expect(find.text('Review & quote'), findsOneWidget);
    expect(find.text('Waiting 24h+'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
