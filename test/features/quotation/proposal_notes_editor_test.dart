import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/quotation/domain/proposal_terms.dart';
import 'package:inspecta/features/quotation/presentation/widgets/proposal_notes_editor.dart';
import 'package:inspecta/l10n/app_localizations.dart';

void main() {
  late List<String> latest;

  Future<void> pump(WidgetTester tester, {List<String>? initial}) async {
    latest = [...(initial ?? defaultProposalNotes)];
    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('en'),
        localizationsDelegates: const [
          AppLocalizations.delegate,
          GlobalMaterialLocalizations.delegate,
          GlobalWidgetsLocalizations.delegate,
          GlobalCupertinoLocalizations.delegate,
        ],
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(
          body: SingleChildScrollView(
            child: ProposalNotesEditor(initial: latest, onChanged: (notes) => latest = notes),
          ),
        ),
      ),
    );
  }

  testWidgets('stays folded, showing how many standard points there are', (tester) async {
    await pump(tester);

    expect(find.text('Standard notes · ${defaultProposalNotes.length} points'), findsOneWidget);
    expect(find.text(defaultProposalNotes.first), findsNothing);
  });

  testWidgets('unfolds to numbered points that can be edited, added and removed', (tester) async {
    await pump(tester, initial: ['First', 'Second']);

    await tester.tap(find.text('Notes on the proposal'));
    await tester.pumpAndSettle();
    expect(find.text('First'), findsOneWidget);
    expect(find.text('Second'), findsOneWidget);

    // Tap a point to edit it in place.
    await tester.tap(find.text('First'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'First'), 'First, edited');
    expect(latest, ['First, edited', 'Second']);

    // A new point opens for typing.
    await tester.tap(find.text('Add a note'));
    await tester.pumpAndSettle();
    expect(latest, ['First, edited', 'Second', '']);
    await tester.enterText(find.byType(TextField), 'Third');
    expect(latest, ['First, edited', 'Second', 'Third']);

    // ✕ removes a point.
    await tester.tap(find.byIcon(Icons.close_rounded).first);
    await tester.pumpAndSettle();
    expect(latest, ['Second', 'Third']);
    // The header counts the points and stops calling them "standard".
    expect(find.text('2 points'), findsOneWidget);
  });

  testWidgets('a point left empty is dropped, and reset restores the standard list', (tester) async {
    await pump(tester, initial: ['Only point']);
    await tester.tap(find.text('Notes on the proposal'));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Add a note'));
    await tester.pumpAndSettle();
    // Leaving it empty and moving focus away removes it.
    FocusManager.instance.primaryFocus?.unfocus();
    await tester.pumpAndSettle();
    expect(latest, ['Only point']);

    await tester.tap(find.text('Reset to standard'));
    await tester.pumpAndSettle();
    expect(latest, defaultProposalNotes);
    expect(find.text('Reset to standard'), findsNothing);
  });
}
