import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_search_field.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/certificate_number.dart';
import '../../../machines/presentation/pages/job_machines_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_source.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/review_queue_cubit.dart';
import '../widgets/review_header.dart';
import '../widgets/sent_certificate_card.dart';

/// Feature 06's Sent tab: every certificate already signed and sent to a
/// client, newest first, grouped by the day it went out and searchable.
/// Reads the [ReviewQueueCubit] the technical manager's shell provides.
class SentCertificatesPage extends StatefulWidget {
  final VoidCallback onOpenProfile;

  const SentCertificatesPage({super.key, required this.onOpenProfile});

  @override
  State<SentCertificatesPage> createState() => _SentCertificatesPageState();
}

class _SentCertificatesPageState extends State<SentCertificatesPage> {
  final _searchController = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  /// The day the job's certificates went out — or, while they're still with
  /// the coordinator, the day they were approved.
  static DateTime _dateOf(InspectionRequest r) =>
      (r.sentAt ?? r.updatedAt ?? r.scheduledAt ?? r.receivedAt).toLocal();

  /// How many certificates a job stands for: its machines, or one when the
  /// queue row carries no counts.
  static int _weight(InspectionRequest r) {
    final total = r.certificates?.total ?? 0;
    return total > 0 ? total : 1;
  }

  bool _matchesQuery(InspectionRequest r) {
    final query = _query.trim().toLowerCase();
    if (query.isEmpty) return true;
    final haystack = [
      certNumberFor(r),
      r.equipmentTitle,
      r.clientName,
      r.inspectorName ?? '',
      r.location,
    ].join('\n').toLowerCase();
    return haystack.contains(query);
  }

  List<InspectionRequest> _visible(List<InspectionRequest> sent) {
    return sent.where(_matchesQuery).toList()
      ..sort((a, b) => _dateOf(b).compareTo(_dateOf(a)));
  }

  /// Sent certificates are read-only: their machines open the PDF, never
  /// the check list.
  Future<void> _open(BuildContext context, InspectionRequest request) {
    return Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => JobMachinesPage(request: request, pdfOnly: true),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark,
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: BlocBuilder<ReviewQueueCubit, ReviewQueueState>(
          builder: (context, state) {
            final ready = state.status == ReviewQueueStatus.ready;
            final sent = state.sent;
            return Column(
              children: [
                ReviewHeader(
                  title: t.sentCertificatesTitle,
                  onOpenProfile: widget.onOpenProfile,
                  child: ready && sent.isNotEmpty
                      ? _Summary(sent: sent, weight: _weight)
                      : null,
                ),
                if (ready && sent.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
                    child: ISearchField(
                      controller: _searchController,
                      onChanged: (value) => setState(() => _query = value),
                      hintText: t.sentSearchHint,
                    ),
                  ),
                Expanded(child: _body(context, state, _visible(sent))),
              ],
            );
          },
        ),
      ),
    );
  }

  Widget _body(
    BuildContext context,
    ReviewQueueState state,
    List<InspectionRequest> visible,
  ) {
    final t = AppLocalizations.of(context)!;
    if (state.status == ReviewQueueStatus.loading) {
      return const _Skeleton();
    }
    if (state.status == ReviewQueueStatus.error) {
      return _ErrorState(failure: state.failure!);
    }
    if (state.sent.isEmpty || visible.isEmpty) {
      return ListEmpty(
        hasSearch: state.sent.isNotEmpty && _query.trim().isNotEmpty,
        hasFilters: false,
        noneTitle: t.emptySentCertificates,
        noMatchTitle: t.sentNoResults,
        searchMessage: t.sentNoResultsHint,
        clearSearchLabel: t.clearAction,
        onClearSearch: () {
          _searchController.clear();
          setState(() => _query = '');
        },
      );
    }

    final locale = Localizations.localeOf(context).languageCode;
    final dayFormat = DateFormat('EEE, d MMM', locale);

    // A header whenever the day, or whether the certificates have gone out
    // yet, changes from one job to the next.
    final children = <Widget>[];
    DateTime? groupDay;
    bool? groupDelivered;
    for (final request in visible) {
      final date = _dateOf(request);
      final day = DateTime(date.year, date.month, date.day);
      final delivered = request.status == JobStatus.sentToClient;
      if (day != groupDay || delivered != groupDelivered) {
        groupDay = day;
        groupDelivered = delivered;
        final label = dayFormat.format(day);
        children.add(
          Padding(
            padding: EdgeInsets.fromLTRB(4, children.isEmpty ? 16 : 20, 4, 10),
            child: Text(
              delivered ? t.sentGroupSent(label) : t.sentGroupApproved(label),
              style: AppTextStyles.subtitle.copyWith(
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        );
      } else {
        children.add(const SizedBox(height: 12));
      }
      children.add(
        SentCertificateCard(
          key: ValueKey(request.id),
          request: request,
          onOpen: () => _open(context, request),
        ),
      );
    }

    return RefreshIndicator(
      color: AppColours.primaryColor,
      onRefresh: context.read<ReviewQueueCubit>().start,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(
          16,
          0,
          16,
          AppLayout.bottomNavClearance,
        ),
        children: children,
      ),
    );
  }
}

/// What the skeleton cards are drawn from; only its shape matters.
final _placeholder = InspectionRequest(
  id: 'placeholder',
  source: RequestSource.email,
  clientName: 'Client name',
  receivedAt: DateTime(2026),
  location: 'Location of the site',
  status: JobStatus.sentToClient,
  items: const [],
  subject: 'Quotation number',
  inspectorName: 'Inspector name',
  scheduledAt: DateTime(2026),
);

/// The tab's loading state: cards drawn from [_placeholder], shimmering.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: ListView.separated(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
        itemCount: 3,
        separatorBuilder: (_, _) => const SizedBox(height: 12),
        itemBuilder: (_, _) =>
            SentCertificateCard(request: _placeholder, onOpen: () {}),
      ),
    );
  }
}

/// "5 certificates · all delivered to clients" under the title: the count of
/// every certificate on the tab, and how many have reached the client.
class _Summary extends StatelessWidget {
  final List<InspectionRequest> sent;
  final int Function(InspectionRequest) weight;

  const _Summary({required this.sent, required this.weight});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    var total = 0;
    var delivered = 0;
    for (final request in sent) {
      total += weight(request);
      if (request.status == JobStatus.sentToClient)
        delivered += weight(request);
    }
    final waiting = total - delivered;

    return Text.rich(
      TextSpan(
        children: [
          TextSpan(
            text: t.sentCertificatesCount(total),
            style: const TextStyle(
              fontWeight: FontWeight.w700,
              color: AppColours.ink,
            ),
          ),
          TextSpan(
            text:
                ' · ${waiting == 0 ? t.sentSummaryAllDelivered : t.sentSummaryMixed(delivered, waiting)}',
          ),
        ],
      ),
      style: AppTextStyles.caption.copyWith(height: 1.3),
    );
  }
}

class _ErrorState extends StatelessWidget {
  final RequestsFailureCode failure;

  const _ErrorState({required this.failure});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            MNotice(type: MNoticeType.error, message: failure.message(t)),
            const SizedBox(height: 16),
            OutlinedButton(
              onPressed: context.read<ReviewQueueCubit>().retry,
              child: Text(t.retry),
            ),
          ],
        ),
      ),
    );
  }
}
