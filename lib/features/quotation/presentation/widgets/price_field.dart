import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/utils/currency.dart';
import '../../../../l10n/app_localizations.dart';

/// An amount field with its currency code at the start (BR-03.2: price is per
/// equipment unit).
///
/// With [onCurrencyChanged] the code is a small menu button — tap it to pick
/// another currency from [Currency.supported]; without it the currency is
/// fixed and shown as plain text.
class PriceField extends StatefulWidget {
  final TextEditingController controller;
  final ValueChanged<String>? onChanged;
  final String? errorText;

  /// ISO code shown before the amount.
  final String currency;

  /// Makes the currency pickable; called with the code the user chose.
  final ValueChanged<String>? onCurrencyChanged;

  const PriceField({
    super.key,
    required this.controller,
    this.onChanged,
    this.errorText,
    this.currency = Currency.egp,
    this.onCurrencyChanged,
  });

  @override
  State<PriceField> createState() => _PriceFieldState();
}

class _PriceFieldState extends State<PriceField> {
  final _focusNode = FocusNode();

  @override
  void initState() {
    super.initState();
    _focusNode.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _focusNode.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final hasError = widget.errorText != null;
    final borderColor = hasError
        ? AppColours.errorIcon
        : _focusNode.hasFocus
        ? AppColours.primaryColor
        : AppColours.border;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          height: 48,
          padding: const EdgeInsetsDirectional.only(start: 8, end: 14),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: borderColor, width: _focusNode.hasFocus ? 2 : 1),
          ),
          child: Row(
            children: [
              _CurrencyPrefix(
                currency: widget.currency,
                onChanged: widget.onCurrencyChanged,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: TextField(
                  controller: widget.controller,
                  onChanged: widget.onChanged,
                  focusNode: _focusNode,
                  keyboardType: const TextInputType.numberWithOptions(decimal: true),
                  inputFormatters: [
                    FilteringTextInputFormatter.allow(RegExp(r'^\d*\.?\d{0,2}')),
                  ],
                  textAlignVertical: TextAlignVertical.center,
                  style: AppTextStyles.input.copyWith(fontWeight: FontWeight.w600),
                  decoration: const InputDecoration(
                    border: InputBorder.none,
                    isCollapsed: true,
                  ),
                ),
              ),
            ],
          ),
        ),
        if (hasError) ...[
          const SizedBox(height: 6),
          Text(
            widget.errorText!,
            style: AppTextStyles.caption.copyWith(
              color: AppColours.errorIcon,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ],
    );
  }
}

/// The code at the start of the field: plain text when the currency is
/// fixed, otherwise a tonal button that opens the list of currencies.
class _CurrencyPrefix extends StatelessWidget {
  final String currency;
  final ValueChanged<String>? onChanged;

  const _CurrencyPrefix({required this.currency, required this.onChanged});

  static const _codeStyle = TextStyle(
    fontSize: 14,
    fontWeight: FontWeight.w700,
    color: AppColours.inkBody,
  );

  @override
  Widget build(BuildContext context) {
    final onChanged = this.onChanged;
    if (onChanged == null) {
      return Padding(
        padding: const EdgeInsetsDirectional.only(start: 6),
        child: Text(currency, style: _codeStyle),
      );
    }

    final t = AppLocalizations.of(context)!;
    return PopupMenuButton<String>(
      tooltip: t.currencyLabel,
      initialValue: currency,
      onSelected: onChanged,
      position: PopupMenuPosition.under,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      itemBuilder: (_) => [
        for (final code in Currency.supported)
          PopupMenuItem<String>(
            value: code,
            child: Text(
              code,
              style: _codeStyle.copyWith(
                color: code == currency ? AppColours.primaryColor : AppColours.ink,
              ),
            ),
          ),
      ],
      child: Container(
        height: 32,
        padding: const EdgeInsetsDirectional.only(start: 10, end: 4),
        decoration: BoxDecoration(
          color: AppColours.surfaceMuted,
          borderRadius: BorderRadius.circular(10),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(currency, style: _codeStyle),
            const Icon(Icons.arrow_drop_down_rounded, size: 20, color: AppColours.inkMuted),
          ],
        ),
      ),
    );
  }
}
