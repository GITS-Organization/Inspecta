import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/quotation_failure.dart';
import 'price_field.dart';
import 'quotation_labels.dart';

/// The bottom sheet behind "Send new price": the new total and how long it
/// stays valid. Sending it creates the quote's next version.
class SendNewPriceSheet extends StatefulWidget {
  /// The version being sent, shown in the title.
  final int nextVersion;

  /// Sends the price; returns why it failed, or `null` when it went through.
  final Future<QuotationFailureCode?> Function({
    required int totalPiastres,
    int? validityDays,
  })
  onSubmit;

  const SendNewPriceSheet({super.key, required this.nextVersion, required this.onSubmit});

  static Future<void> show(
    BuildContext context, {
    required int nextVersion,
    required Future<QuotationFailureCode?> Function({
      required int totalPiastres,
      int? validityDays,
    })
    onSubmit,
  }) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => SendNewPriceSheet(nextVersion: nextVersion, onSubmit: onSubmit),
    );
  }

  @override
  State<SendNewPriceSheet> createState() => _SendNewPriceSheetState();
}

class _SendNewPriceSheetState extends State<SendNewPriceSheet> {
  static const _validityOptions = [14, 30];

  final _priceController = TextEditingController();
  int _validityDays = _validityOptions.first;
  bool _submitted = false;
  bool _submitting = false;

  @override
  void dispose() {
    _priceController.dispose();
    super.dispose();
  }

  /// The entered total in piastres, or `null` when empty or zero.
  int? get _totalPiastres {
    final cleaned = _priceController.text.replaceAll(',', '').trim();
    final pounds = double.tryParse(cleaned);
    if (pounds == null || pounds <= 0) return null;
    return (pounds * 100).round();
  }

  Future<void> _submit() async {
    final t = AppLocalizations.of(context)!;
    setState(() => _submitted = true);
    final total = _totalPiastres;
    if (total == null) return;

    setState(() => _submitting = true);
    final failure = await widget.onSubmit(totalPiastres: total, validityDays: _validityDays);
    if (!mounted) return;
    if (failure == null) {
      MToast.showSuccess(message: t.newPriceSent);
      Navigator.of(context).pop();
    } else {
      setState(() => _submitting = false);
      MToast.showError(message: failure.message(t));
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
      child: Container(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 24),
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
        ),
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Center(
                child: Container(
                  width: 40,
                  height: 4,
                  margin: const EdgeInsets.only(bottom: 16),
                  decoration: BoxDecoration(
                    color: AppColours.border,
                    borderRadius: BorderRadius.circular(999),
                  ),
                ),
              ),
              Text(
                t.sendNewPriceVersion(t.quoteVersionLabel(widget.nextVersion)),
                style: AppTextStyles.cardTitle.copyWith(fontSize: 18),
              ),
              const SizedBox(height: 16),
              Text(t.newTotalPriceLabel, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
              const SizedBox(height: 6),
              PriceField(
                controller: _priceController,
                onChanged: (_) => setState(() {}),
                errorText: _submitted && _totalPiastres == null ? t.errorTotalPriceRequired : null,
              ),
              const SizedBox(height: 14),
              Text(t.validFor, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
              const SizedBox(height: 6),
              Wrap(
                spacing: 8,
                children: [
                  for (final days in _validityOptions)
                    ChoiceChip(
                      label: Text(t.validityDaysOption(days)),
                      selected: _validityDays == days,
                      onSelected: (_) => setState(() => _validityDays = days),
                    ),
                ],
              ),
              const SizedBox(height: 18),
              MPrimaryButton(
                label: t.sendNewPriceVersion(t.quoteVersionLabel(widget.nextVersion)),
                loading: _submitting,
                onPressed: _submitting ? null : _submit,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
