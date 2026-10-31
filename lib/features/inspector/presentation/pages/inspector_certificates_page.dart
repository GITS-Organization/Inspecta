import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_search_field.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../../../machines/presentation/pages/job_machines_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
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
  State<InspectorCertificatesPage> createState() => _InspectorCertificatesPageState();
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
      MaterialPageRoute<void>(builder: (_) => JobMachinesPage(request: request)),
    );
    cubit.start();
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<CertificatesListCubit, CertificatesListState>(
      builder: (context, state) {
        if (state.status == CertificatesListStatus.loading) {
          return Loading.loader(context);
        }
        if (state.status == CertificatesListStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final visible = state.visible;
        if (visible.isEmpty) {
          return Center(child: Text(t.emptyCertificatesList, style: AppTextStyles.subtitle));
        }

        return RefreshIndicator(
          color: AppColours.primaryColor,
          onRefresh: context.read<CertificatesListCubit>().start,
          child: ListView.separated(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(16, 0, 16, AppLayout.bottomNavClearance),
            itemCount: visible.length,
            separatorBuilder: (_, _) => const SizedBox(height: 12),
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
