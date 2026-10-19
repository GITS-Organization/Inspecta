import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/auth/domain/entities/user.dart';
import 'package:inspecta/features/auth/domain/entities/user_role.dart';
import 'package:inspecta/features/auth/domain/usecases/get_current_user.dart';
import 'package:inspecta/features/auth/domain/usecases/sign_out.dart';
import 'package:inspecta/features/auth/presentation/bloc/auth_cubit.dart';
import 'package:inspecta/features/coordinator/domain/queue_filter.dart';
import 'package:inspecta/features/coordinator/domain/repositories/coordinator_repository.dart';
import 'package:inspecta/features/coordinator/domain/usecases/get_inspectors.dart';
import 'package:inspecta/features/coordinator/presentation/bloc/coordinator_queue_cubit.dart';
import 'package:inspecta/features/coordinator/presentation/pages/coordinator_queue_page.dart';
import 'package:inspecta/features/requests/domain/entities/inspection_request.dart';
import 'package:inspecta/features/requests/domain/entities/request_item.dart';
import 'package:inspecta/features/requests/domain/usecases/get_requests.dart';
import 'package:inspecta/injection.dart';
import 'package:inspecta/l10n/app_localizations.dart';

import '../auth/fake_auth_repository.dart';
import '../requests/fake_requests_repository.dart';

class _FakeCoordinatorRepository extends Fake implements CoordinatorRepository {
  @override
  Stream<List<AppUser>> watchInspectors() => Stream.value(const []);
}

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

/// One job per group, told apart by their equipment: a crane waiting on an
/// inspector, a lift scheduled, a boiler in progress — plus a request that
/// isn't in the queue at all.
List<InspectionRequest> _jobs() {
  RequestItem item(String type) => RequestItem(id: type, type: type, capacity: '5 t', quantity: 1);
  final at = DateTime(2026, 11, 3, 9);
  return [
    sampleRequest(id: 'REQ-1', status: JobStatus.quoteAccepted, items: [item('Crane')]),
    sampleRequest(id: 'REQ-2', status: JobStatus.assigned, scheduledAt: at, items: [item('Lift')]),
    sampleRequest(id: 'REQ-3', status: JobStatus.inProgress, scheduledAt: at, items: [item('Boiler')]),
    sampleRequest(id: 'REQ-4', status: JobStatus.requestReceived, items: [item('Hoist')]),
  ];
}

void main() {
  late FakeRequestsRepository repo;

  setUp(() {
    repo = FakeRequestsRepository();
  });

  CoordinatorQueueCubit newCubit() =>
      CoordinatorQueueCubit(GetRequests(repo), GetInspectors(_FakeCoordinatorRepository()));

  group('CoordinatorQueueCubit filter', () {
    late CoordinatorQueueCubit cubit;

    setUp(() async {
      cubit = newCubit()..start();
      repo.controller.add(_jobs());
      await pumpEventQueue();
    });

    tearDown(() => cubit.close());

    List<String> ids() => cubit.state.jobs.map((r) => r.id).toList();

    test('shows every job in the queue until a filter is picked', () {
      expect(cubit.state.filter, isNull);
      expect(ids(), unorderedEquals(['REQ-1', 'REQ-2', 'REQ-3']));
    });

    test('narrows the list to the picked group', () {
      cubit.toggleFilter(QueueFilter.unassigned);
      expect(ids(), ['REQ-1']);

      cubit.toggleFilter(QueueFilter.scheduled);
      expect(ids(), ['REQ-2']);

      cubit.toggleFilter(QueueFilter.inProgress);
      expect(ids(), ['REQ-3']);
    });

    test('picking the group that is already on shows every job again', () {
      cubit.toggleFilter(QueueFilter.scheduled);
      cubit.toggleFilter(QueueFilter.scheduled);

      expect(cubit.state.filter, isNull);
      expect(ids(), unorderedEquals(['REQ-1', 'REQ-2', 'REQ-3']));
    });

    test('the tile counts stay whole-queue whichever filter is on', () {
      cubit.toggleFilter(QueueFilter.scheduled);

      expect(cubit.state.unassignedCount, 1);
      expect(cubit.state.scheduledCount, 1);
      expect(cubit.state.inProgressCount, 1);
    });

    test('keeps the filter when the requests stream updates', () async {
      cubit.toggleFilter(QueueFilter.inProgress);
      repo.controller.add(_jobs());
      await pumpEventQueue();

      expect(cubit.state.filter, QueueFilter.inProgress);
      expect(ids(), ['REQ-3']);
    });
  });

  group('CoordinatorQueuePage', () {
    late AuthCubit authCubit;

    setUp(() {
      authCubit = _SignedInAuthCubit(_coordinator, FakeAuthRepository());
      getIt.registerFactory<CoordinatorQueueCubit>(newCubit);
    });

    tearDown(() async {
      await authCubit.close();
      await getIt.reset();
    });

    Future<void> pumpPage(WidgetTester tester) async {
      // Wider than a phone: the test font (Ahem) draws every glyph a full em
      // wide, which would overflow the list's heading row at 390 — nothing
      // this test is about.
      tester.view.physicalSize = const Size(800, 1200);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);

      await tester.pumpWidget(
        BlocProvider<AuthCubit>.value(
          value: authCubit,
          child: MaterialApp(
            localizationsDelegates: AppLocalizations.localizationsDelegates,
            supportedLocales: AppLocalizations.supportedLocales,
            home: CoordinatorQueuePage(onOpenProfile: () {}),
          ),
        ),
      );
      repo.controller.add(_jobs());
      await tester.pump();
      await tester.pump();
    }

    Finder job(String equipment) => find.textContaining(equipment);

    // The header's tile is the first widget carrying the label; the job
    // cards' status chips can repeat it further down.
    Finder tile(String label) => find.text(label).first;

    testWidgets('tapping a header tile narrows the list, and tapping it again widens it', (tester) async {
      await pumpPage(tester);

      for (final equipment in ['Crane', 'Lift', 'Boiler']) {
        expect(job(equipment), findsOneWidget, reason: equipment);
      }
      expect(job('Hoist'), findsNothing);

      await tester.tap(tile('Scheduled'));
      await tester.pump();

      expect(job('Lift'), findsOneWidget);
      expect(job('Crane'), findsNothing);
      expect(job('Boiler'), findsNothing);

      await tester.tap(tile('Scheduled'));
      await tester.pump();

      for (final equipment in ['Crane', 'Lift', 'Boiler']) {
        expect(job(equipment), findsOneWidget, reason: equipment);
      }
    });

    testWidgets('switching tiles swaps the group, and an empty group says so', (tester) async {
      await pumpPage(tester);

      await tester.tap(tile('Unassigned'));
      await tester.pump();
      expect(job('Crane'), findsOneWidget);
      expect(job('Lift'), findsNothing);

      await tester.tap(tile('In progress'));
      await tester.pump();
      expect(job('Boiler'), findsOneWidget);
      expect(job('Crane'), findsNothing);

      repo.controller.add([sampleRequest(id: 'REQ-1', status: JobStatus.quoteAccepted)]);
      await tester.pump();
      await tester.pump();
      expect(find.text('No jobs in this group right now.'), findsOneWidget);
    });
  });
}
