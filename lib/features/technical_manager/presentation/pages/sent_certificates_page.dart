import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../machines/presentation/pages/job_machines_page.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../../requests/domain/entities/requests_failure.dart';
import '../../../requests/presentation/widgets/requests_labels.dart';
import '../bloc/review_queue_cubit.dart';
import '../widgets/review_header.dart';
import '../widgets/sent_certificate_card.dart';

/// Feature 06's Sent tab: every certificate already signed and sent to a
/// client, newest first. Reads the [ReviewQueueCubit] the technical manager's
/// shell provides.
class SentCertificatesPage extends StatelessWidget {
  final VoidCallback onOpenProfile;

  const SentCertificatesPage({super.key, required this.onOpenProfile});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark,
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            ReviewHeader(title: t.sentCertificatesTitle, onOpenProfile: onOpenProfile),
            const Expanded(child: _Body()),
          ],
        ),
      ),
    );
  }
}

class _Body extends StatelessWidget {
  const _Body();

  /// Sent certificates are read-only: their machines open the PDF, never
  /// the check list.
  Future<void> _open(BuildContext context, InspectionRequest request) {
    return Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => JobMachinesPage(request: request, pdfOnly: true)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<ReviewQueueCubit, ReviewQueueState>(
      builder: (context, state) {
        if (state.status == ReviewQueueStatus.loading) {
          return Loading.loader(context);
        }
        if (state.status == ReviewQueueStatus.error) {
          return _ErrorState(failure: state.failure!);
        }

        final sent = state.sent;
        if (sent.isEmpty) {
          return Center(child: Text(t.emptySentCertificates, style: AppTextStyles.subtitle));
        }
        return ListView.separated(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, AppLayout.bottomNavClearance),
          itemCount: sent.length,
          separatorBuilder: (_, _) => const SizedBox(height: 12),
          itemBuilder: (context, index) => SentCertificateCard(
            key: ValueKey(sent[index].id),
            request: sent[index],
            onOpen: () => _open(context, sent[index]),
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
            OutlinedButton(onPressed: context.read<ReviewQueueCubit>().retry, child: Text(t.retry)),
          ],
        ),
      ),
    );
  }
}
