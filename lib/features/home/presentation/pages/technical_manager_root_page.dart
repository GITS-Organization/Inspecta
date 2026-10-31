import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../injection.dart';
import '../../../auth/presentation/pages/profile_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../technical_manager/presentation/bloc/review_queue_cubit.dart';
import '../../../technical_manager/presentation/pages/review_queue_page.dart';
import '../../../technical_manager/presentation/pages/sent_certificates_page.dart';
import '../../../technical_manager/presentation/widgets/review_bottom_nav.dart';

/// The technical manager's shell: Review, Sent and Profile — same
/// in-place-swap shape as `CoordinatorRootPage`.
class TechnicalManagerRootPage extends StatefulWidget {
  const TechnicalManagerRootPage({super.key});

  @override
  State<TechnicalManagerRootPage> createState() =>
      _TechnicalManagerRootPageState();
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
    TechnicalManagerTab.sent => SentCertificatesPage(
      onOpenProfile: _openProfile,
    ),
    TechnicalManagerTab.profile => Builder(
      builder: (context) => ProfilePage(certifyQueue: _certifyQueue(context.read<ReviewQueueCubit>())),
    ),
  };

  /// The shell's queue as the Profile's stats read it: what is loaded now,
  /// then every change.
  Stream<List<InspectionRequest>> _certifyQueue(ReviewQueueCubit queue) async* {
    yield queue.state.requests;
    yield* queue.stream.map((state) => state.requests);
  }

  @override
  Widget build(BuildContext context) {
    // One queue for the shell: the Review and Sent tabs (and the Profile's
    // counts) read the same `status=certify` list instead of each fetching it.
    return BlocProvider(
      create: (_) => getIt<ReviewQueueCubit>()..start(),
      child: Scaffold(
        body: IndexedStack(
          index: _tab.index,
          children: [
            for (final tab in TechnicalManagerTab.values)
              _opened.contains(tab) ? _page(tab) : const SizedBox.shrink(),
          ],
        ),
        bottomNavigationBar: ReviewBottomNav(
          selected: _tab,
          onSelectTab: _select,
        ),
      ),
    );
  }
}
