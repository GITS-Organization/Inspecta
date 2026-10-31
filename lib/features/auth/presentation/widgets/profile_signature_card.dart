import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/widgets/signature_pad.dart';
import '../../../../l10n/app_localizations.dart';
import '../bloc/my_signature_cubit.dart';

/// The technical manager's "My signature" card on the Profile screen: the
/// signature that's put on every certificate they approve, and a button to
/// draw a new one. Needs a [MySignatureCubit] above it.
class ProfileSignatureCard extends StatelessWidget {
  const ProfileSignatureCard({super.key});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return BlocBuilder<MySignatureCubit, MySignatureState>(
      builder: (context, state) {
        final signature = state.signature;
        return MCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(child: Text(t.mySignatureTitle, style: AppTextStyles.sectionTitle)),
                  if (state.loaded)
                    TextButton(
                      onPressed: () => _showEditSheet(context),
                      style: TextButton.styleFrom(
                        backgroundColor: AppColours.primaryTint,
                        foregroundColor: AppColours.primaryDark,
                        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
                      ),
                      child: Text(
                        signature == null ? t.addSignatureAction : t.updateAction,
                        style: const TextStyle(fontWeight: FontWeight.w700),
                      ),
                    ),
                ],
              ),
              const SizedBox(height: 12),
              Container(
                height: 96,
                width: double.infinity,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: AppColours.surfaceMuted,
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(color: AppColours.border),
                ),
                child: !state.loaded
                    ? const SizedBox.square(dimension: 22, child: CircularProgressIndicator(strokeWidth: 2.5))
                    : signature == null
                    ? Text(t.noSignatureLabel, style: AppTextStyles.caption)
                    : Padding(
                        padding: const EdgeInsets.all(8),
                        child: Image.memory(
                          base64Decode(signature.substring(signature.indexOf(',') + 1)),
                          fit: BoxFit.contain,
                          gaplessPlayback: true,
                        ),
                      ),
              ),
              const SizedBox(height: 10),
              Text(t.savedSignatureHint, style: AppTextStyles.caption),
            ],
          ),
        );
      },
    );
  }

  Future<void> _showEditSheet(BuildContext context) {
    final cubit = context.read<MySignatureCubit>();
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(28))),
      builder: (_) => BlocProvider.value(value: cubit, child: const _SignatureEditSheet()),
    );
  }
}

/// Draw a signature, then Save — it replaces whatever was saved before.
class _SignatureEditSheet extends StatefulWidget {
  const _SignatureEditSheet();

  @override
  State<_SignatureEditSheet> createState() => _SignatureEditSheetState();
}

class _SignatureEditSheetState extends State<_SignatureEditSheet> {
  List<List<double>> _strokes = const [];

  Future<void> _save() async {
    final t = AppLocalizations.of(context)!;
    final navigator = Navigator.of(context);
    final saved = await context.read<MySignatureCubit>().save(_strokes);
    if (saved) {
      navigator.pop();
    } else {
      MToast.showError(message: t.errorUnknown);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final isSaving = context.select((MySignatureCubit cubit) => cubit.state.isSaving);
    final canSave = !isSaving && _strokes.any((stroke) => stroke.length >= 4);
    return Padding(
      padding: EdgeInsets.fromLTRB(20, 20, 20, 20 + MediaQuery.viewInsetsOf(context).bottom),
      child: SafeArea(
        top: false,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(child: Text(t.signatureSheetTitle, style: AppTextStyles.cardTitle.copyWith(fontSize: 19))),
                TextButton(
                  onPressed: !isSaving && _strokes.isNotEmpty ? () => setState(() => _strokes = const []) : null,
                  style: TextButton.styleFrom(
                    backgroundColor: AppColours.primarySoft,
                    foregroundColor: AppColours.primaryDark,
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                  ),
                  child: Text(t.clearAction, style: const TextStyle(fontWeight: FontWeight.w700)),
                ),
              ],
            ),
            const SizedBox(height: 6),
            Text(t.signatureSheetHint, style: AppTextStyles.caption),
            const SizedBox(height: 14),
            SizedBox(
              height: 180,
              child: SignaturePad(
                strokes: _strokes,
                onChanged: (strokes) => setState(() => _strokes = strokes),
                enabled: !isSaving,
              ),
            ),
            const SizedBox(height: 16),
            SizedBox(
              width: double.infinity,
              height: 50,
              child: FilledButton(
                onPressed: canSave ? _save : null,
                style: FilledButton.styleFrom(
                  backgroundColor: AppColours.primaryColor,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                ),
                child: isSaving
                    ? const SizedBox.square(
                        dimension: 20,
                        child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white),
                      )
                    : Text(t.saveAction, style: AppTextStyles.buttonLabel.copyWith(fontSize: 15)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
