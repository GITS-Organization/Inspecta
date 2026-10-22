import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/app_filter_chip.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/certificates_list_cubit.dart';

/// The Certificates tab's one filter row: Action / Submitted / Sent / All,
/// as chips with a live count. Action is the inspector's to-do list, so its
/// count is red when there is one (Feature 05).
class CertificatesFilterTabs extends StatelessWidget {
  const CertificatesFilterTabs({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<CertificatesListCubit, CertificatesListState>(
      builder: (context, state) {
        final cubit = context.read<CertificatesListCubit>();
        Widget chip(CertificatesFilter filter, String label, {int count = 0, bool alert = false}) => AppFilterChip(
          label: label,
          count: count,
          alert: alert,
          selected: state.filter == filter,
          onTap: () => cubit.setFilter(filter),
        );

        return SizedBox(
          height: 40,
          child: ListView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            children: [
              chip(CertificatesFilter.action, t.filterActionTab, count: state.actionCount, alert: true),
              const SizedBox(width: 8),
              chip(CertificatesFilter.submitted, t.filterSubmittedTab, count: state.submittedCount),
              const SizedBox(width: 8),
              chip(CertificatesFilter.sent, t.filterSentTab, count: state.sentCount),
              const SizedBox(width: 8),
              chip(CertificatesFilter.all, t.filterAll),
            ],
          ),
        );
      },
    );
  }
}
