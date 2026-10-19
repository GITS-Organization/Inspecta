import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/features/auth/domain/entities/user.dart';
import 'package:inspecta/features/auth/domain/entities/user_role.dart';
import 'package:inspecta/features/auth/presentation/bloc/auth_cubit.dart';
import 'package:inspecta/features/certificate/presentation/pdf/certificate_pdf_preview.dart';
import 'package:inspecta/l10n/app_localizations.dart';
import 'package:intl/date_symbol_data_local.dart';

import 'certificate_pdf_test.dart' show airCompressorCertificate, airCompressorRequest;

/// Only `user` matters to the preview helper; the rest keeps BlocProvider happy.
class _FakeAuthCubit extends Fake implements AuthCubit {
  _FakeAuthCubit(this._user);
  final AppUser? _user;

  @override
  AppUser? get user => _user;
  @override
  AuthState get state => const AuthUnknown();
  @override
  Stream<AuthState> get stream => const Stream.empty();
  @override
  Future<void> close() async {}
}

/// Only a technical manager may download a certificate's PDF; everyone else
/// (inspectors included) gets a view-only preview.
void main() {
  setUpAll(() => initializeDateFormatting('en'));

  Future<void> open(WidgetTester tester, AppUser? user) async {
    tester.view.physicalSize = const Size(800, 1600);
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(BlocProvider<AuthCubit>.value(
      value: _FakeAuthCubit(user),
      child: MaterialApp(
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Builder(builder: (context) => Scaffold(
          body: ElevatedButton(
            onPressed: () => previewCertificatePdf(context, request: airCompressorRequest(), certificate: airCompressorCertificate()),
            child: const Text('open'),
          ),
        )),
      ),
    ));
    await tester.tap(find.text('open'));
    // One frame to start the route's slide-in, one to finish it — until then
    // the page is offstage and the finders can't see it.
    await tester.pump(const Duration(milliseconds: 600));
    await tester.pump(const Duration(milliseconds: 600));
  }

  AppUser userWith(UserRole role) => AppUser(id: 'u', name: 'Test User', email: 't@x.test', role: role);

  testWidgets('inspector gets a view-only preview', (tester) async {
    await open(tester, userWith(UserRole.inspector));
    expect(find.text('Report of thorough examination'), findsOneWidget);
    expect(find.text('Download PDF'), findsNothing);
    expect(find.byType(FilledButton), findsNothing);
  });

  testWidgets('technical manager gets the download button', (tester) async {
    await open(tester, userWith(UserRole.technicalManager));
    expect(find.text('Download PDF'), findsOneWidget);
  });

  for (final role in [UserRole.coordinator, UserRole.admin]) {
    testWidgets('$role gets no download', (tester) async {
      await open(tester, userWith(role));
      expect(find.text('Download PDF'), findsNothing);
    });
  }

  testWidgets('no signed-in user gets no download', (tester) async {
    await open(tester, null);
    expect(find.text('Download PDF'), findsNothing);
  });
}
