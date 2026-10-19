import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:inspecta/core/enums/job_status.dart';
import 'package:inspecta/features/auth/domain/entities/user.dart';
import 'package:inspecta/features/auth/domain/entities/user_role.dart';
import 'package:inspecta/features/auth/presentation/bloc/auth_cubit.dart';
import 'package:inspecta/features/inspector/presentation/bloc/inspector_tasks_cubit.dart';
import 'package:inspecta/features/inspector/presentation/pages/inspector_tasks_page.dart';
import 'package:inspecta/features/requests/domain/usecases/accept_task.dart';
import 'package:inspecta/features/requests/domain/usecases/decline_task.dart';
import 'package:inspecta/features/requests/domain/usecases/get_my_tasks.dart';
import 'package:inspecta/features/requests/domain/usecases/start_inspection.dart';
import 'package:inspecta/injection.dart';
import 'package:inspecta/l10n/app_localizations.dart';
import 'package:intl/date_symbol_data_local.dart';

import '../requests/fake_requests_repository.dart';

class _FakeAuthCubit extends Fake implements AuthCubit {
  @override
  AppUser? get user => const AppUser(id: 'insp-1', name: 'Karim Adel', email: 'k@x.test', role: UserRole.inspector);
  @override
  AuthState get state => const AuthUnknown();
  @override
  Stream<AuthState> get stream => const Stream.empty();
  @override
  Future<void> close() async {}
}

void main() {
  late FakeRequestsRepository repo;

  setUpAll(() => initializeDateFormatting('en'));

  setUp(() {
    repo = FakeRequestsRepository();
    getIt.registerFactory(
      () => InspectorTasksCubit(
        inspectorId: 'insp-1',
        getMyTasks: GetMyTasks(repo),
        acceptTask: AcceptTask(repo),
        declineTask: DeclineTask(repo),
        startInspection: StartInspection(repo),
      ),
    );
  });

  tearDown(() async {
    await repo.assignedController.close();
    await getIt.reset();
  });

  DateTime at(int daysFromToday, int hour) {
    final today = DateTime.now();
    return DateTime(today.year, today.month, today.day + daysFromToday, hour);
  }

  Future<void> open(WidgetTester tester) async {
    tester.view.physicalSize = const Size(390, 900) * 2;
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(
      BlocProvider<AuthCubit>.value(
        value: _FakeAuthCubit(),
        child: MaterialApp(
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          locale: const Locale('en'),
          home: const InspectorTasksPage(),
        ),
      ),
    );
    repo.assignedController.add([
      sampleRequest(id: 'today-1', status: JobStatus.taskAccepted, clientName: 'Today Co.', scheduledAt: at(0, 9)),
      sampleRequest(id: 'today-2', status: JobStatus.inProgress, clientName: 'Also Today', scheduledAt: at(0, 13)),
      sampleRequest(id: 'tomorrow', status: JobStatus.taskAccepted, clientName: 'Tomorrow Co.', scheduledAt: at(1, 10)),
      sampleRequest(id: 'next-week', status: JobStatus.taskAccepted, clientName: 'Next Week Co.', scheduledAt: at(8, 10)),
      sampleRequest(id: 'late', status: JobStatus.taskAccepted, clientName: 'Late Co.', scheduledAt: at(-3, 9)),
      sampleRequest(id: 'done', status: JobStatus.certificateSubmitted, clientName: 'Done Co.', scheduledAt: at(0, 8)),
    ]);
    await tester.pumpAndSettle();
  }

  // Equipment is the same on every sample request, so the cards are told
  // apart by the order they are listed in and by their count.
  int cards() => find.text('Overhead crane — 10 t').evaluate().length;

  /// The count shown on a header tile.
  Finder count(String label, int n) => find.descendant(
    of: find.ancestor(of: find.text(label), matching: find.byType(InkWell)),
    matching: find.text('$n'),
  );

  testWidgets('starts on Today, with a tile for each list and its count', (tester) async {
    await open(tester);

    expect(find.text('Today'), findsOneWidget);
    expect(find.text('Upcoming'), findsOneWidget);
    expect(find.text('Overdue'), findsOneWidget);
    expect(count('Today', 2), findsOneWidget);
    expect(count('Upcoming', 2), findsOneWidget);
    expect(count('Overdue', 1), findsOneWidget);
    // Today's two jobs only: not the later ones, and not finished work.
    expect(cards(), 2);
    expect(find.textContaining('days overdue'), findsNothing);
  });

  testWidgets('has no day strip any more', (tester) async {
    await open(tester);

    expect(find.text('Mon'), findsNothing);
    expect(find.textContaining('tasks on'), findsNothing);
  });

  testWidgets('Upcoming lists what is coming, grouped under the day it is due', (tester) async {
    await open(tester);
    await tester.tap(find.text('Upcoming'));
    await tester.pumpAndSettle();

    expect(cards(), 2);
    expect(find.text('Tomorrow'), findsOneWidget);
    expect(find.textContaining('days overdue'), findsNothing);
  });

  testWidgets('Overdue lists what was missed, and says how late', (tester) async {
    await open(tester);
    await tester.tap(find.text('Overdue'));
    await tester.pumpAndSettle();

    expect(cards(), 1);
    expect(find.text('3 days overdue'), findsOneWidget);
  });

  testWidgets('says so when a list is empty', (tester) async {
    await open(tester);
    repo.assignedController.add([sampleRequest(id: 'late', status: JobStatus.taskAccepted, scheduledAt: at(-1, 9))]);
    await tester.pumpAndSettle();

    expect(find.text('Nothing scheduled for today.'), findsOneWidget);

    await tester.tap(find.text('Upcoming'));
    await tester.pumpAndSettle();
    expect(find.text('No upcoming tasks.'), findsOneWidget);

    await tester.tap(find.text('Overdue'));
    await tester.pumpAndSettle();
    expect(find.text('1 day overdue'), findsOneWidget);
  });

  testWidgets('the new-task nudge takes you to a job in another list', (tester) async {
    await open(tester);
    repo.assignedController.add([
      sampleRequest(id: 'today', status: JobStatus.taskAccepted, scheduledAt: at(0, 9)),
      sampleRequest(id: 'new', status: JobStatus.assigned, clientName: 'New Co.', scheduledAt: at(3, 9)),
    ]);
    await tester.pumpAndSettle();

    // It is for something that is not on Today, yet it is offered there.
    expect(find.text('New task '), findsNothing); // rendered inside one rich text
    expect(find.textContaining('New task', findRichText: true), findsOneWidget);
    expect(find.text('Needs response'), findsNothing);

    await tester.tap(find.text('Open'));
    await tester.pumpAndSettle();

    expect(find.text('Needs response'), findsOneWidget);
    expect(find.text('Accept'), findsOneWidget);
  });
}
