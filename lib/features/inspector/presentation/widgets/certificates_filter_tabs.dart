import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/app_segmented_filter.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/certificates_list_cubit.dart';

/// The Certificates tab's one filter row: All / Submitted / Sent, with a
/// live count on the last two (Feature 05). The inspector's to-do list is
/// on the Tasks tab.
class CertificatesFilterTabs extends StatelessWidget {
  const CertificatesFilterTabs({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<CertificatesListCubit, CertificatesListState>(
      builder: (context, state) {
        return AppSegmentedFilter<CertificatesFilter>(
          segments: [
            AppSegment(value: CertificatesFilter.all, label: t.filterAll),
            AppSegment(
              value: CertificatesFilter.submitted,
              label: t.filterSubmittedTab,
              count: state.submittedCount,
            ),
            AppSegment(value: CertificatesFilter.sent, label: t.filterSentTab, count: state.sentCount),
          ],
          selected: state.filter,
          onChanged: context.read<CertificatesListCubit>().setFilter,
        );
      },
    );
  }
}
