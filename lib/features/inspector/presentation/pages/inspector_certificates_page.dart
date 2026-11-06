import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/enums/job_status.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_search_field.dart';
import '../../../../core/widgets/banner_art.dart';
import '../../../../core/widgets/empty_widget.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../../../machines/presentation/pages/job_machines_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/request_source.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/certificates_list_cubit.dart';
import '../widgets/certificate_list_card.dart';
import '../widgets/certificates_filter_tabs.dart';

/// Feature 05's Certificates tab: every job with a certificate underway
/// or done, bucketed by status, searchable.
class InspectorCertificatesPage extends StatefulWidget {
  final VoidCallback? onOpenProfile;

  const InspectorCertificatesPage({super.key, this.onOpenProfile});

  @override
  State<InspectorCertificatesPage> createState() =>
      _InspectorCertificatesPageState();
}

class _InspectorCertificatesPageState extends State<InspectorCertificatesPage> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocProvider(
      create: (_) => getIt<CertificatesListCubit>()..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: Scaffold(
          backgroundColor: AppColours.background,
          body: Column(
            children: [
              Builder(
                builder: (context) => UserAppHeader(
                  title: t.myCertificatesTitle,
                  onOpenProfile: widget.onOpenProfile,
                  style: AppBannerStyle.rings,
                  bottom: ISearchField(
                    controller: _searchController,
                    onChanged: context.read<CertificatesListCubit>().setQuery,
                    hintText: t.searchCertificatesHint,
                  ),
                ),
              ),
              const SizedBox(height: 12),
              const CertificatesFilterTabs(),
              const SizedBox(height: 12),
              const Expanded(child: _Body()),
            ],
          ),
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body();

  // Submitting a machine moves the job on, so the list reloads once the user
  // is back from it.
  Future<void> _open(BuildContext context, InspectionRequest request) async {
    final cubit = context.read<CertificatesListCubit>();
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => JobMachinesPage(request: request),
      ),
    );
    cubit.start();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<CertificatesListCubit, CertificatesListState>(
      builder: (context, state) {
        if (state.status == CertificatesListStatus.loading) {
          return const _Skeleton();
        }
        if (state.status == CertificatesListStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final visible = state.visible;
        final cubit = context.read<CertificatesListCubit>();

        // A list with nothing to show still pulls to refresh, and says why it
        // is empty: a search that matched nothing, a tab with nothing in it,
        // or no certificates at all.
        if (visible.isEmpty) {
          final searching = state.query.trim().isNotEmpty;
          final kind = searching
              ? EmptyKind.search
              : state.filter == CertificatesFilter.all
              ? EmptyKind.nodata
              : EmptyKind.filter;
          return RefreshIndicator(
            color: AppColours.primaryColor,
            onRefresh: cubit.start,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(
                16,
                0,
                16,
                AppLayout.bottomNavClearance,
              ),
              children: [
                EmptyState(
                  kind: kind,
                  icon: kind == EmptyKind.nodata
                      ? Icons.workspace_premium_outlined
                      : null,
                  title: t.emptyCertificatesList,
                ),
              ],
            ),
          );
        }

        return RefreshIndicator(
          color: AppColours.primaryColor,
          onRefresh: cubit.start,
          child: ListView.separated(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(
              16,
              2,
              16,
              AppLayout.bottomNavClearance,
            ),
            itemCount: visible.length,
            separatorBuilder: (_, _) => const SizedBox(height: 14),
            itemBuilder: (context, index) {
              final request = visible[index];
              return CertificateListCard(
                key: ValueKey(request.id),
                request: request,
                onOpen: () => _open(context, request),
              );
            },
          ),
        );
      },
    );
  }
}

/// What the skeleton's cards are drawn from; only their shape matters.
final _placeholder = InspectionRequest(
  id: 'placeholder',
  source: RequestSource.email,
  clientName: 'Client name',
  subject: 'QT-2026-000000 rev01',
  receivedAt: DateTime(2026),
  location: 'Location of the site',
  status: JobStatus.inProgress,
  items: const [],
  scheduledAt: DateTime(2026, 1, 1, 9, 30),
);

/// The loading state, in the shape of the list: three certificate cards drawn
/// by the real widget from [_placeholder], shimmering.
class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    return Skeletonizer(
      child: ListView(
        physics: const NeverScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 2, 16, 0),
        children: [
          for (var i = 0; i < 3; i++) ...[
            CertificateListCard(request: _placeholder, onOpen: () {}),
            const SizedBox(height: 14),
          ],
        ],
      ),
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
              onPressed: context.read<CertificatesListCubit>().retry,
              child: Text(t.retry),
            ),
          ],
        ),
      ),
    );
  }
}
