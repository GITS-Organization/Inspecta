import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_layout.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/loading.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/timesheet_failure.dart';
import '../../domain/entities/timesheet_row.dart';
import '../bloc/timesheet_cubit.dart';
import '../widgets/timesheet_entry_sheet.dart';
import '../widgets/timesheet_header.dart';
import '../widgets/timesheet_labels.dart';
import '../widgets/timesheet_task_card.dart';

/// The inspector's Timesheet tab: the jobs they've accepted this month,
/// each with the time they spent and the price they set for it.
class InspectorTimesheetPage extends StatelessWidget {
  final VoidCallback? onOpenProfile;

  const InspectorTimesheetPage({super.key, this.onOpenProfile});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<TimesheetCubit>()..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: Scaffold(
          backgroundColor: AppColours.background,
          body: Column(
            children: [
              TimesheetHeader(onOpenProfile: onOpenProfile),
              Expanded(
                child: BlocBuilder<TimesheetCubit, TimesheetState>(
                  builder: (context, state) => switch (state.status) {
                    TimesheetStatus.loading => Loading.loader(context),
                    TimesheetStatus.error => _ErrorState(failure: state.failure!),
                    TimesheetStatus.ready => _TimesheetList(rows: state.rows, hourlyRatePiastres: state.hourlyRatePiastres),
                  },
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _TimesheetList extends StatelessWidget {
  final List<TimesheetRow> rows;

  /// What new entries are priced with.
  final int hourlyRatePiastres;

  const _TimesheetList({required this.rows, required this.hourlyRatePiastres});

  static bool _sameDay(DateTime a, DateTime b) => a.year == b.year && a.month == b.month && a.day == b.day;

  Future<void> _edit(BuildContext context, TimesheetRow row) async {
    final cubit = context.read<TimesheetCubit>();
    final t = AppLocalizations.of(context)!;
    await TimesheetEntrySheet.show(
      context,
      row: row,
      hourlyRatePiastres: hourlyRatePiastres,
      onSubmit: ({required activities, required expenses}) async {
        final failure = await cubit.saveEntry(
          requestId: row.request.id,
          activities: activities,
          expenses: expenses,
        );
        if (failure != null) {
          MToast.showError(message: failure.message(t));
          return false;
        }
        MToast.showSuccess(message: t.timesheetSavedMessage);
        return true;
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    if (rows.isEmpty) {
      return Center(child: Text(t.timesheetEmptyMonth, style: AppTextStyles.subtitle));
    }

    final loggedCount = rows.where((row) => row.isLogged).length;
    final children = <Widget>[
      Padding(
        padding: const EdgeInsets.symmetric(horizontal: 4),
        child: Text(
          t.timesheetLoggedProgress(loggedCount, rows.length),
          style: AppTextStyles.caption.copyWith(fontWeight: FontWeight.w600),
        ),
      ),
    ];

    DateTime? currentDay;
    for (final row in rows) {
      final day = row.request.scheduledAt!;
      if (currentDay == null || !_sameDay(currentDay, day)) {
        children.add(
          Padding(
            padding: EdgeInsets.only(left: 4, top: currentDay == null ? 12 : 20, bottom: 12),
            child: Text(
              DateFormat('EEEE, d MMMM', locale).format(day),
              style: AppTextStyles.sectionTitle,
            ),
          ),
        );
        currentDay = day;
      } else {
        children.add(const SizedBox(height: 12));
      }
      children.add(TimesheetTaskCard(key: ValueKey(row.request.id), row: row, onEdit: () => _edit(context, row)));
    }

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, AppLayout.bottomNavClearance),
      children: children,
    );
  }
}

class _ErrorState extends StatelessWidget {
  final TimesheetFailureCode failure;

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
            OutlinedButton(onPressed: context.read<TimesheetCubit>().retry, child: Text(t.retry)),
          ],
        ),
      ),
    );
  }
}
