import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/technical_manager/presentation/widgets/signature_card.dart';
import 'package:inspecta/features/technical_manager/presentation/widgets/signature_pad.dart';
import 'package:inspecta/l10n/app_localizations.dart';

const _saved = [
  [0.2, 0.8, 0.5, 0.3, 0.9, 0.7],
];

Future<void> _pump(WidgetTester tester, {required List<List<double>>? savedStrokes}) {
  return tester.pumpWidget(
    MaterialApp(
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: Scaffold(
        body: SignatureCard(
          savedStrokes: savedStrokes,
          strokes: const [],
          onStrokesChanged: (_) {},
          onClear: () {},
          rememberSignature: true,
          onRememberChanged: (_) {},
        ),
      ),
    ),
  );
}

void main() {
  testWidgets('shows the saved signature instead of the pad, and no name or license fields', (tester) async {
    await _pump(tester, savedStrokes: _saved);

    expect(find.byType(SignaturePreview), findsOneWidget);
    expect(find.byType(SignaturePad), findsNothing);
    expect(find.text('Your signature'), findsOneWidget);
    expect(find.text('Sign here'), findsNothing);
    expect(find.text('Full name'), findsNothing);
    expect(find.text('License no.'), findsNothing);
  });

  testWidgets('asks for a signature when none is saved, offering to remember it', (tester) async {
    await _pump(tester, savedStrokes: const []);

    expect(find.byType(SignaturePad), findsOneWidget);
    expect(find.byType(SignaturePreview), findsNothing);
    expect(find.text('Sign here'), findsOneWidget);
    expect(find.text('Remember my signature for next time'), findsOneWidget);
    expect(find.text('Full name'), findsNothing);
    expect(find.text('License no.'), findsNothing);
  });

  testWidgets('waits, without showing the pad, until the saved signature has loaded', (tester) async {
    await _pump(tester, savedStrokes: null);

    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(find.byType(SignaturePad), findsNothing);
  });
}
