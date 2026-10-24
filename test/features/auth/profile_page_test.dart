import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/framework/app_cubit.dart';
import 'package:inspecta/core/framework/app_preferences.dart';
import 'package:inspecta/features/auth/domain/entities/user.dart';
import 'package:inspecta/features/auth/domain/entities/user_role.dart';
import 'package:inspecta/features/auth/domain/usecases/get_current_user.dart';
import 'package:inspecta/features/auth/domain/usecases/sign_out.dart';
import 'package:inspecta/features/auth/presentation/bloc/auth_cubit.dart';
import 'package:inspecta/features/auth/presentation/bloc/profile_stats_cubit.dart';
import 'package:inspecta/features/auth/presentation/pages/profile_page.dart';
import 'package:inspecta/features/certificate/domain/repositories/certificate_repository.dart';
import 'package:inspecta/features/certificate/domain/usecases/watch_inspector_certificates.dart';
import 'package:inspecta/features/certificate/domain/usecases/watch_reviewed_certificates.dart';
import 'package:inspecta/features/clients/domain/repositories/clients_repository.dart';
import 'package:inspecta/features/clients/domain/usecases/get_clients.dart';
import 'package:inspecta/features/quotation/domain/usecases/get_quotations.dart';
import 'package:inspecta/features/requests/domain/usecases/get_my_tasks.dart';
import 'package:inspecta/features/requests/domain/usecases/get_requests.dart';
import 'package:inspecta/injection.dart';
import 'package:inspecta/l10n/app_localizations.dart';

import '../quotation/fake_quotation_repository.dart';
import '../requests/fake_requests_repository.dart';
import 'fake_auth_repository.dart';

class _FakeClientsRepository extends Fake implements ClientsRepository {}

class _FakeCertificateRepository extends Fake implements CertificateRepository {}

/// The real cubit, but with no Firestore streams behind it: the tiles stay
/// at their empty state, which is all this screen's layout needs.
class _QuietStatsCubit extends ProfileStatsCubit {
  _QuietStatsCubit()
    : super(
        GetQuotations(FakeQuotationRepository()),
        GetClients(_FakeClientsRepository()),
        GetRequests(FakeRequestsRepository()),
        WatchReviewedCertificates(_FakeCertificateRepository()),
        GetMyTasks(FakeRequestsRepository()),
        WatchInspectorCertificates(_FakeCertificateRepository()),
      );

  @override
  void start(AppUser? user) {}
}

/// An [AuthCubit] that is already signed in as [user].
class _SignedInAuthCubit extends AuthCubit {
  _SignedInAuthCubit(AppUser user, FakeAuthRepository repo) : super(GetCurrentUser(repo), SignOut(repo)) {
    emit(Authenticated(user));
  }
}

const _coordinator = AppUser(
  id: 'c1',
  name: 'Dana Mostafa',
  email: 'dana@company.com',
  role: UserRole.coordinator,
);

void main() {
  late AuthCubit authCubit;
  late AppCubit appCubit;

  setUp(() {
    authCubit = _SignedInAuthCubit(_coordinator, FakeAuthRepository());
    appCubit = AppCubit(AppPreferences(const FlutterSecureStorage()));
    getIt.registerFactory<ProfileStatsCubit>(_QuietStatsCubit.new);
  });

  tearDown(() async {
    await authCubit.close();
    await appCubit.close();
    await getIt.reset();
  });

  Future<void> pumpPage(WidgetTester tester, {Widget? navigationSection}) async {
    tester.view.physicalSize = const Size(390, 844) * 3;
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(
      MultiBlocProvider(
        providers: [
          BlocProvider<AuthCubit>.value(value: authCubit),
          BlocProvider<AppCubit>.value(value: appCubit),
        ],
        child: MaterialApp(
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: ProfilePage(navigationSection: navigationSection),
        ),
      ),
    );
  }

  testWidgets('shows who is signed in', (tester) async {
    await pumpPage(tester);

    expect(find.text('Dana Mostafa'), findsOneWidget);
    expect(find.text('dana@company.com'), findsOneWidget);
    expect(find.text('Coordinator'), findsOneWidget);
  });

  testWidgets('puts the navigation section ahead of the account card', (tester) async {
    await pumpPage(tester, navigationSection: const Text('NAV SECTION'));

    double top(String text) => tester.getTopLeft(find.text(text, skipOffstage: false)).dy;

    expect(top('NAV SECTION'), lessThan(top('Account')));
  });

  testWidgets('a coordinator has no role card (no quotation defaults)', (tester) async {
    await pumpPage(tester);

    expect(find.text('Quotation defaults', skipOffstage: false), findsNothing);
  });

  testWidgets('a coordinator has no "My areas" card', (tester) async {
    await pumpPage(tester);

    expect(find.text('My areas', skipOffstage: false), findsNothing);
  });

  testWidgets('has no navigation section unless one is passed', (tester) async {
    await pumpPage(tester);

    expect(find.text('NAV SECTION'), findsNothing);
    expect(find.text('Account', skipOffstage: false), findsOneWidget);
  });
}
