import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/utils/currency.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/presentation/widgets/user_app_header.dart';
import '../bloc/timesheet_cubit.dart';
import 'hourly_rate_sheet.dart';
import 'timesheet_labels.dart';

/// The Timesheet tab's header: the shared banner, with the month switcher and
/// what the inspector has logged that month — total price and total time —
/// under the title, and the hourly rate they're worked out from.
class TimesheetHeader extends StatelessWidget {
  final VoidCallback? onOpenProfile;

  const TimesheetHeader({super.key, this.onOpenProfile});

  Future<void> _editRate(BuildContext context, int current) async {
    final cubit = context.read<TimesheetCubit>();
    final t = AppLocalizations.of(context)!;
    await HourlyRateSheet.show(
      context,
      initialRatePiastres: current,
      onSubmit: (rate) async {
        final failure = await cubit.saveHourlyRate(rate);
        if (failure != null) {
          MToast.showError(message: failure.message(t));
          return false;
        }
        MToast.showSuccess(message: t.hourlyRateSavedMessage);
        return true;
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return UserAppHeader(
      title: t.timesheetTitle,
      onOpenProfile: onOpenProfile,
      bottom: BlocBuilder<TimesheetCubit, TimesheetState>(
        builder: (context, state) {
          final cubit = context.read<TimesheetCubit>();
          return Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              _MonthSwitcher(month: state.selectedMonth, onPrevious: cubit.previousMonth, onNext: cubit.nextMonth),
              const SizedBox(height: 12),
              // Stretch both tiles to the taller one, so the total's note
              // doesn't leave the time tile shorter.
              IntrinsicHeight(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Expanded(
                      flex: 3,
                      child: _StatTile(
                        label: t.timesheetTotalPriceLabel,
                        value: Currency.formatEgp(state.totalPiastres),
                        // Costs are part of the total but not of the price, so
                        // say how much of it they are.
                        note: state.totalExpensesPiastres > 0
                            ? t.timesheetIncludingTransportation(Currency.formatEgp(state.totalExpensesPiastres))
                            : null,
                        emphasised: true,
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      flex: 2,
                      child: _StatTile(
                        label: t.timesheetTimeLoggedLabel,
                        value: state.totalMinutes == 0 ? t.durationHours(0) : formatMinutes(t, state.totalMinutes),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 8),
              _RateRow(
                hourlyRatePiastres: state.hourlyRatePiastres,
                onTap: () => _editRate(context, state.hourlyRatePiastres),
              ),
            ],
          );
        },
      ),
    );
  }
}

/// The inspector's hourly rate — what every price below is worked out from —
/// tappable to change it, or a prompt to set one if they haven't.
class _RateRow extends StatelessWidget {
  final int hourlyRatePiastres;
  final VoidCallback onTap;

  const _RateRow({required this.hourlyRatePiastres, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final hasRate = hourlyRatePiastres > 0;
    return Material(
      color: hasRate ? Colors.white : AppColours.chipAmberBackground,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: BorderSide(color: hasRate ? AppColours.border : AppColours.chipAmberBackground),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          child: Row(
            children: [
              Icon(
                Icons.payments_outlined,
                size: 18,
                color: hasRate ? AppColours.primaryDark : AppColours.chipAmberText,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  hasRate
                      ? '${t.hourlyRateLabel} · ${t.hourlyRatePerHour(Currency.formatEgp(hourlyRatePiastres))}'
                      : t.hourlyRateSetPrompt,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppTextStyles.emphasis.copyWith(
                    color: hasRate ? AppColours.ink : AppColours.chipAmberText,
                    fontSize: 14,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
              Icon(
                hasRate ? Icons.edit_outlined : Icons.chevron_right_rounded,
                size: 18,
                color: hasRate ? AppColours.inkMuted : AppColours.chipAmberText,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _MonthSwitcher extends StatelessWidget {
  final DateTime month;
  final VoidCallback onPrevious;
  final VoidCallback onNext;

  const _MonthSwitcher({required this.month, required this.onPrevious, required this.onNext});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).languageCode;
    return Container(
      height: 48,
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColours.border),
      ),
      child: Row(
        children: [
          IconButton(
            onPressed: onPrevious,
            tooltip: t.previousMonthTooltip,
            icon: const Icon(Icons.chevron_left_rounded, color: AppColours.ink),
          ),
          Expanded(
            child: Text(
              DateFormat('MMMM yyyy', locale).format(month),
              textAlign: TextAlign.center,
              style: AppTextStyles.cardTitle,
            ),
          ),
          IconButton(
            onPressed: onNext,
            tooltip: t.nextMonthTooltip,
            icon: const Icon(Icons.chevron_right_rounded, color: AppColours.ink),
          ),
        ],
      ),
    );
  }
}

/// A figure tile: the emphasised one is filled navy (the month's total),
/// the other white.
class _StatTile extends StatelessWidget {
  final String label;
  final String value;

  /// A smaller line under the label.
  final String? note;
  final bool emphasised;

  const _StatTile({required this.label, required this.value, this.note, this.emphasised = false});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: emphasised ? AppColours.navy : Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: emphasised ? null : Border.all(color: AppColours.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          // A long month total would otherwise overflow its tile.
          FittedBox(
            fit: BoxFit.scaleDown,
            alignment: AlignmentDirectional.centerStart,
            child: Text(
              value,
              style: TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.w700,
                color: emphasised ? Colors.white : AppColours.ink,
              ),
            ),
          ),
          Text(
            label,
            overflow: TextOverflow.ellipsis,
            style: AppTextStyles.caption.copyWith(
              fontSize: 12,
              height: 16 / 12,
              fontWeight: FontWeight.w600,
              color: emphasised ? AppColours.onNavyMuted : AppColours.inkMuted,
            ),
          ),
          if (note != null)
            Text(
              note!,
              overflow: TextOverflow.ellipsis,
              style: AppTextStyles.caption.copyWith(
                fontSize: 11,
                color: emphasised ? AppColours.onNavyMuted : AppColours.inkMuted,
              ),
            ),
        ],
      ),
    );
  }
}
