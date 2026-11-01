import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/shared/m_primary_button.dart';
import '../../../../core/utils/currency.dart';
import '../../../../l10n/app_localizations.dart';
import '../../domain/entities/client_response_outcome.dart';
import 'reply_option_tile.dart' show ReplyOptionTile;
import 'price_field.dart';

/// The bottom sheet behind Respond / "Log a client reply": choose the
/// outcome (BR-03.6), then whatever it needs — the client's price for a
/// counter, an optional note for the rest.
class LogClientReplySheet extends StatefulWidget {
  final Future<void> Function({
    required ClientResponseOutcome outcome,
    int? clientPricePiastres,
    String? note,
  })
  onSubmit;

  /// The quote's currency: the client's counter price is in the same one.
  final String currency;

  const LogClientReplySheet({super.key, required this.onSubmit, this.currency = Currency.egp});

  static Future<void> show(
    BuildContext context, {
    String currency = Currency.egp,
    required Future<void> Function({
      required ClientResponseOutcome outcome,
      int? clientPricePiastres,
      String? note,
    })
    onSubmit,
  }) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => LogClientReplySheet(onSubmit: onSubmit, currency: currency),
    );
  }

  @override
  State<LogClientReplySheet> createState() => _LogClientReplySheetState();
}

class _LogClientReplySheetState extends State<LogClientReplySheet> {
  final _priceController = TextEditingController();
  final _noteController = TextEditingController();
  ClientResponseOutcome? _outcome;
  bool _submitted = false;
  bool _submitting = false;

  @override
  void dispose() {
    _priceController.dispose();
    _noteController.dispose();
    super.dispose();
  }

  int? get _clientPrice {
    final cleaned = _priceController.text.replaceAll(',', '').trim();
    if (cleaned.isEmpty) return null;
    final pounds = double.tryParse(cleaned);
    return pounds == null ? null : (pounds * 100).round();
  }

  bool get _priceMissing => _outcome == ClientResponseOutcome.counter && _clientPrice == null;

  Future<void> _submit() async {
    if (_outcome == null) return;
    setState(() => _submitted = true);
    if (_priceMissing) return;

    setState(() => _submitting = true);
    await widget.onSubmit(
      outcome: _outcome!,
      clientPricePiastres: _clientPrice,
      note: _noteController.text.trim().isEmpty ? null : _noteController.text.trim(),
    );
    if (mounted) Navigator.of(context).pop();
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
              Text(t.logClientReplyTitle, style: AppTextStyles.cardTitle.copyWith(fontSize: 18)),
              const SizedBox(height: 16),
              ReplyOptionTile(
                title: t.outcomeCounter,
                subtitle: t.outcomeCounterSubtitle,
                selected: _outcome == ClientResponseOutcome.counter,
                onTap: () => setState(() => _outcome = ClientResponseOutcome.counter),
              ),
              const SizedBox(height: 10),
              ReplyOptionTile(
                title: t.outcomeAccepted,
                subtitle: t.outcomeAcceptedSubtitle,
                selected: _outcome == ClientResponseOutcome.accepted,
                onTap: () => setState(() => _outcome = ClientResponseOutcome.accepted),
              ),
              const SizedBox(height: 10),
              ReplyOptionTile(
                title: t.outcomeDeclined,
                subtitle: t.outcomeDeclinedSubtitle,
                danger: true,
                selected: _outcome == ClientResponseOutcome.declined,
                onTap: () => setState(() => _outcome = ClientResponseOutcome.declined),
              ),
              if (_outcome == ClientResponseOutcome.counter) ...[
                const SizedBox(height: 14),
                Text(t.clientPriceLabel, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
                const SizedBox(height: 6),
                PriceField(
                  currency: widget.currency,
                  controller: _priceController,
                  onChanged: (_) => setState(() {}),
                  errorText: _submitted && _priceMissing ? t.errorClientPriceRequired : null,
                ),
              ],
              if (_outcome != null) ...[
                const SizedBox(height: 14),
                Text(t.noteOptionalLabel, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
                const SizedBox(height: 6),
                TextField(
                  controller: _noteController,
                  minLines: 2,
                  maxLines: 4,
                  style: AppTextStyles.input.copyWith(fontWeight: FontWeight.w400),
                  decoration: InputDecoration(
                    isDense: true,
                    contentPadding: const EdgeInsets.all(12),
                    filled: true,
                    fillColor: Colors.white,
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(12),
                      borderSide: const BorderSide(color: AppColours.border, width: 1.5),
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 18),
              MPrimaryButton(
                label: t.logReply,
                loading: _submitting,
                onPressed: _outcome == null ? null : _submit,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
