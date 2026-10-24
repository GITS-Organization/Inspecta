import 'package:flutter/material.dart';
import 'package:intl/intl.dart' show DateFormat;
import 'package:table_calendar/table_calendar.dart';

import '../../l10n/app_localizations.dart';
import '../consts/app_colors.dart';
import '../consts/app_text_styles.dart';

class AppDatePicker extends StatefulWidget {
  final bool _isRange;
  final String? title;
  final DateTime? initialDate;
  final DateTime? firstDate;
  final DateTime? lastDate;

  const AppDatePicker._({
    required this._isRange,
    this.title,
    this.initialDate,
    this.firstDate,
    this.lastDate,
  });

  /// Picks one day. [initialDate] (default today, clamped into the allowed
  /// span) starts out selected, so Confirm is enabled straight away.
  static Future<DateTime?> pickDate(
    BuildContext context, {
    String? title,
    DateTime? initialDate,
    DateTime? firstDate,
    DateTime? lastDate,
  }) {
    return showDialog<DateTime>(
      context: context,
      builder: (_) => AppDatePicker._(
        isRange: false,
        title: title,
        initialDate: initialDate,
        firstDate: firstDate,
        lastDate: lastDate,
      ),
    );
  }

  /// Picks a start and end day: tap a start day, then an end day. Confirming
  /// with only a start day picked returns a single-day range.
  static Future<DateTimeRange?> pickRange(
    BuildContext context, {
    String? title,
    DateTime? firstDate,
    DateTime? lastDate,
  }) {
    return showDialog<DateTimeRange>(
      context: context,
      builder: (_) => AppDatePicker._(
        isRange: true,
        title: title,
        firstDate: firstDate,
        lastDate: lastDate,
      ),
    );
  }

  @override
  State<AppDatePicker> createState() => _AppDatePickerState();
}

class _AppDatePickerState extends State<AppDatePicker> {
  late final DateTime _first = _dateOnly(widget.firstDate ?? DateTime.now());
  late final DateTime _last = _dateOnly(
    widget.lastDate ?? DateTime(DateTime.now().year + 2),
  );
  late DateTime _focusedDay = _clamp(widget.initialDate ?? DateTime.now());
  late DateTime? _startDate = widget._isRange ? null : _focusedDay;
  DateTime? _endDate;

  static DateTime _dateOnly(DateTime d) => DateTime(d.year, d.month, d.day);

  DateTime _clamp(DateTime d) {
    final day = _dateOnly(d);
    if (day.isBefore(_first)) return _first;
    if (day.isAfter(_last)) return _last;
    return day;
  }

  void _onFocusedDayChanged(DateTime day) {
    setState(() => _focusedDay = _clamp(day));
  }

  void _onDaySelected(DateTime day, DateTime focusedDay) {
    setState(() {
      _startDate = _dateOnly(day);
      _focusedDay = _clamp(focusedDay);
    });
  }

  void _onRangeSelected(DateTime? start, DateTime? end, DateTime focusedDay) {
    setState(() {
      _startDate = start == null ? null : _dateOnly(start);
      _endDate = end == null ? null : _dateOnly(end);
      _focusedDay = _clamp(focusedDay);
    });
  }

  void _confirm() {
    final start = _startDate;
    if (start == null) return;
    Navigator.of(context).pop<Object>(
      widget._isRange
          ? DateTimeRange(start: start, end: _endDate ?? start)
          : start,
    );
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final locale = Localizations.localeOf(context).toString();
    final isRange = widget._isRange;

    return Dialog(
      backgroundColor: AppColours.background,
      surfaceTintColor: Colors.transparent,
      clipBehavior: Clip.antiAlias,
      insetPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            _Header(
              title:
                  widget.title ??
                  (isRange ? t.dateRangeTitle : t.selectDateHint),
              locale: locale,
              startLabel: isRange ? t.dateRangeStartLabel : t.dateLabel,
              startDate: _startDate,
              endLabel: isRange ? t.dateRangeEndLabel : null,
              endDate: _endDate,
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(8, 16, 8, 0),
              child: _Calendar(
                locale: locale,
                firstDay: _first,
                lastDay: _last,
                focusedDay: _focusedDay,
                isRange: isRange,
                start: _startDate,
                end: _endDate,
                onFocusedDayChanged: _onFocusedDayChanged,
                onDaySelected: _onDaySelected,
                onRangeSelected: _onRangeSelected,
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
              child: isRange
                  ? _HintCard(
                      message: _startDate == null
                          ? t.dateRangeSelectHint
                          : _endDate == null
                          ? t.dateRangePickEndHint
                          : t.dateRangeReadyHint,
                    )
                  : null,
            ),
            const Divider(height: 1, color: AppColours.border),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Expanded(
                    child: SizedBox(
                      height: 52,
                      child: OutlinedButton(
                        onPressed: () => Navigator.of(context).pop(),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: AppColours.inkBody,
                          backgroundColor: Colors.white,
                          side: const BorderSide(
                            color: AppColours.border,
                            width: 1.5,
                          ),
                          textStyle: AppTextStyles.buttonLabel.copyWith(
                            fontSize: 14,
                          ),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(14),
                          ),
                        ),
                        child: Text(t.cancel),
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: SizedBox(
                      height: 52,
                      child: FilledButton(
                        onPressed: _startDate == null ? null : _confirm,
                        style: FilledButton.styleFrom(
                          backgroundColor: AppColours.primaryColor,
                          disabledBackgroundColor: AppColours.primaryColor
                              .withValues(alpha: 0.5),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(14),
                          ),
                        ),
                        child: Text(
                          t.confirmAction,
                          style: AppTextStyles.buttonLabel.copyWith(
                            fontSize: 14,
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Blue header: the title and the picked date(s) — one tile for a single
/// date, two for a range. The tile for the date still to be picked is
/// highlighted.
class _Header extends StatelessWidget {
  final String title;
  final String locale;
  final String startLabel;
  final DateTime? startDate;

  /// Null for a single-date picker, which shows only the start tile.
  final String? endLabel;
  final DateTime? endDate;

  const _Header({
    required this.title,
    required this.locale,
    required this.startLabel,
    required this.startDate,
    required this.endLabel,
    required this.endDate,
  });

  String? _format(DateTime? date) =>
      date == null ? null : DateFormat('EEE, d MMM yyyy', locale).format(date);

  @override
  Widget build(BuildContext context) {
    final isRange = endLabel != null;
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 18),
      color: AppColours.primaryColor,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            title,
            style: AppTextStyles.pageTitle.copyWith(
              color: Colors.white,
              fontSize: 22,
            ),
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: _DateTile(
                  label: startLabel,
                  value: _format(startDate),
                  active: !isRange || startDate == null,
                ),
              ),
              if (isRange) ...[
                const SizedBox(width: 8),
                Expanded(
                  child: _DateTile(
                    label: endLabel!,
                    value: _format(endDate),
                    active: startDate != null && endDate == null,
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}

class _DateTile extends StatelessWidget {
  final String label;
  final String? value;
  final bool active;

  const _DateTile({
    required this.label,
    required this.value,
    required this.active,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: active ? Colors.white : Colors.white.withValues(alpha: 0.16),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            label,
            style: AppTextStyles.caption.copyWith(
              fontWeight: FontWeight.w600,
              color: active
                  ? AppColours.inkSecondary
                  : AppColours.onPrimaryMuted,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            value ?? '—',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 15,
              fontWeight: FontWeight.w800,
              color: active ? AppColours.primaryDark : Colors.white,
            ),
          ),
        ],
      ),
    );
  }
}

/// White card with the "what to do next" line shown under a range calendar.
class _HintCard extends StatelessWidget {
  final String message;

  const _HintCard({required this.message});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          const Icon(
            Icons.info_outline_rounded,
            size: 20,
            color: AppColours.primaryDark,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              message,
              style: AppTextStyles.caption.copyWith(
                color: AppColours.inkBody,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Month navigation plus the month grid, drawn as rounded day tiles: white
/// for a free day, tinted for a day inside the range, solid blue for the
/// selected / start / end day, and faded for days outside the month or the
/// allowed span. The week starts on Sunday, like the weekly schedule.
class _Calendar extends StatelessWidget {
  final String locale;
  final DateTime firstDay;
  final DateTime lastDay;
  final DateTime focusedDay;
  final bool isRange;

  /// The selected day (single mode) or the range start (range mode).
  final DateTime? start;
  final DateTime? end;
  final ValueChanged<DateTime> onFocusedDayChanged;
  final void Function(DateTime day, DateTime focusedDay) onDaySelected;
  final void Function(DateTime? start, DateTime? end, DateTime focusedDay)
  onRangeSelected;

  const _Calendar({
    required this.locale,
    required this.firstDay,
    required this.lastDay,
    required this.focusedDay,
    required this.isRange,
    required this.start,
    required this.end,
    required this.onFocusedDayChanged,
    required this.onDaySelected,
    required this.onRangeSelected,
  });

  static final TextStyle _dayStyle = AppTextStyles.input.copyWith(
    fontSize: 15,
    fontWeight: FontWeight.w600,
  );
  static const double _tileRadius = 10;

  /// `table_calendar` works in UTC dates; the dialog's state in local ones.
  static DateTime _utc(DateTime d) => DateTime.utc(d.year, d.month, d.day);

  /// Orders calendar days regardless of time zone or time of day.
  static int _key(DateTime d) => d.year * 10000 + d.month * 100 + d.day;

  Widget _dayTile(
    BuildContext context,
    DateTime day,
    DateTime focused,
    DateFormat dayFormat,
  ) {
    final key = _key(day);
    final isOutside = day.month != focused.month;
    final isDisabled = key < _key(firstDay) || key > _key(lastDay);
    final isToday = key == _key(DateTime.now());

    final startKey = start == null ? null : _key(start!);
    final endKey = end == null ? null : _key(end!);
    final isEdge = isRange ? key == startKey || key == endKey : key == startKey;
    final isInRange =
        isRange &&
        startKey != null &&
        endKey != null &&
        key > startKey &&
        key < endKey;

    final Color? fill;
    final Color textColor;
    final FontWeight weight;
    if (isOutside || isDisabled) {
      fill = null;
      textColor = AppColours.inkMuted.withValues(alpha: 0.45);
      weight = FontWeight.w600;
    } else if (isEdge) {
      fill = AppColours.primaryColor;
      textColor = Colors.white;
      weight = FontWeight.w800;
    } else if (isInRange) {
      fill = AppColours.primaryTint;
      textColor = AppColours.chipBlueText;
      weight = FontWeight.w800;
    } else {
      fill = Colors.white;
      textColor = isToday ? AppColours.primaryDark : AppColours.ink;
      weight = isToday ? FontWeight.w800 : FontWeight.w600;
    }

    final showTodayRing = isToday && !isOutside && !isDisabled && !isEdge;

    return Container(
      margin: const EdgeInsets.all(3),
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: fill,
        borderRadius: BorderRadius.circular(_tileRadius),
        border: showTodayRing
            ? Border.all(color: AppColours.primaryColor, width: 1.5)
            : null,
      ),
      child: Text(
        dayFormat.format(day),
        style: _dayStyle.copyWith(color: textColor, fontWeight: weight),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final dayFormat = DateFormat.d(locale);
    final dowStyle = AppTextStyles.fieldLabel.copyWith(fontSize: 13);

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        _CalendarHeader(
          locale: locale,
          focusedDay: focusedDay,
          firstDay: firstDay,
          lastDay: lastDay,
          onChanged: onFocusedDayChanged,
        ),
        const SizedBox(height: 8),
        TableCalendar(
          locale: locale,
          firstDay: _utc(firstDay),
          lastDay: _utc(lastDay),
          focusedDay: _utc(focusedDay),
          headerVisible: false,
          selectedDayPredicate: isRange
              ? null
              : (day) => start != null && _key(day) == _key(start!),
          onDaySelected: onDaySelected,
          rangeStartDay: isRange && start != null ? _utc(start!) : null,
          rangeEndDay: isRange && end != null ? _utc(end!) : null,
          rangeSelectionMode: isRange
              ? RangeSelectionMode.toggledOn
              : RangeSelectionMode.toggledOff,
          onRangeSelected: onRangeSelected,
          onPageChanged: onFocusedDayChanged,
          startingDayOfWeek: StartingDayOfWeek.sunday,
          availableGestures: AvailableGestures.horizontalSwipe,
          rowHeight: 48,
          daysOfWeekHeight: 32,
          daysOfWeekStyle: DaysOfWeekStyle(
            weekdayStyle: dowStyle,
            weekendStyle: dowStyle,
          ),
          calendarStyle: const CalendarStyle(outsideDaysVisible: true),
          calendarBuilders: CalendarBuilders(
            prioritizedBuilder: (context, day, focused) =>
                _dayTile(context, day, focused, dayFormat),
          ),
        ),
      ],
    );
  }
}

/// Previous / next month buttons around month and year dropdowns, so a far
/// date (a certificate's 2000s examination date) is a couple of taps away.
class _CalendarHeader extends StatelessWidget {
  final String locale;
  final DateTime focusedDay;
  final DateTime firstDay;
  final DateTime lastDay;

  /// Called with a day in the month/year to show (the caller clamps it).
  final ValueChanged<DateTime> onChanged;

  const _CalendarHeader({
    required this.locale,
    required this.focusedDay,
    required this.firstDay,
    required this.lastDay,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final monthFormat = DateFormat.MMMM(locale);
    final yearFormat = DateFormat.y(locale);
    // The "previous" button sits at the start edge, so in RTL its arrow
    // must point right.
    final isRtl = Directionality.of(context) == TextDirection.rtl;

    final year = focusedDay.year;
    final month = focusedDay.month;
    final canGoBack = year * 12 + month > firstDay.year * 12 + firstDay.month;
    final canGoForward = year * 12 + month < lastDay.year * 12 + lastDay.month;

    // Only offer months that fall inside the allowed span for this year.
    final firstMonth = year == firstDay.year ? firstDay.month : 1;
    final lastMonth = year == lastDay.year ? lastDay.month : 12;

    return Row(
      children: [
        _NavButton(
          icon: isRtl
              ? Icons.chevron_right_rounded
              : Icons.chevron_left_rounded,
          tooltip: t.previousMonthTooltip,
          onTap: canGoBack ? () => onChanged(DateTime(year, month - 1)) : null,
        ),
        const SizedBox(width: 8),
        Expanded(
          flex: 3,
          child: _HeaderDropdown(
            value: month,
            options: [
              for (var m = firstMonth; m <= lastMonth; m++)
                (m, monthFormat.format(DateTime(2000, m))),
            ],
            onChanged: (m) => onChanged(DateTime(year, m)),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          flex: 2,
          child: _HeaderDropdown(
            value: year,
            options: [
              for (var y = firstDay.year; y <= lastDay.year; y++)
                (y, yearFormat.format(DateTime(y))),
            ],
            onChanged: (y) => onChanged(DateTime(y, month)),
          ),
        ),
        const SizedBox(width: 8),
        _NavButton(
          icon: isRtl
              ? Icons.chevron_left_rounded
              : Icons.chevron_right_rounded,
          tooltip: t.nextMonthTooltip,
          onTap: canGoForward
              ? () => onChanged(DateTime(year, month + 1))
              : null,
        ),
      ],
    );
  }
}

class _NavButton extends StatelessWidget {
  final IconData icon;
  final String tooltip;
  final VoidCallback? onTap;

  const _NavButton({
    required this.icon,
    required this.tooltip,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return IconButton(
      onPressed: onTap,
      tooltip: tooltip,
      style: IconButton.styleFrom(
        fixedSize: const Size.square(40),
        backgroundColor: Colors.white,
        disabledBackgroundColor: Colors.white.withValues(alpha: 0.6),
      ),
      icon: Icon(
        icon,
        color: onTap == null ? AppColours.border : AppColours.inkBody,
      ),
    );
  }
}

/// White rounded chip holding a month or year dropdown. The open menu
/// scrolls to the current value, which matters for a long year list.
class _HeaderDropdown extends StatelessWidget {
  final int value;
  final List<(int, String)> options;
  final ValueChanged<int> onChanged;

  const _HeaderDropdown({
    required this.value,
    required this.options,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 40,
      padding: const EdgeInsetsDirectional.only(start: 12, end: 2),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
      ),
      child: DropdownButtonHideUnderline(
        child: DropdownButton<int>(
          value: value,
          isDense: true,
          isExpanded: true,
          menuMaxHeight: 300,
          dropdownColor: Colors.white,
          borderRadius: BorderRadius.circular(12),
          icon: const Icon(
            Icons.arrow_drop_down_rounded,
            color: AppColours.primaryColor,
          ),
          style: AppTextStyles.emphasis.copyWith(
            fontSize: 15,
            fontWeight: FontWeight.w700,
          ),
          items: [
            for (final (optionValue, label) in options)
              DropdownMenuItem(
                value: optionValue,
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
          ],
          onChanged: (picked) {
            if (picked != null) onChanged(picked);
          },
        ),
      ),
    );
  }
}
