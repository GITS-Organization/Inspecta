import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../certificate/domain/entities/certificate.dart';
import '../../../certificate/domain/usecases/watch_certificate.dart';
import '../../../requests/domain/entities/inspection_request.dart';

/// A certificate this manager sent back: the equipment and client, with
/// the reason given to the inspector underneath.
class ReturnedCertificateRow extends StatelessWidget {
  final InspectionRequest request;
  final VoidCallback onOpen;

  const ReturnedCertificateRow({super.key, required this.request, required this.onOpen});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return StreamBuilder<Certificate?>(
      stream: getIt<WatchCertificate>()(request.id),
      builder: (context, snapshot) {
        final note = snapshot.data?.reviewNote?.trim() ?? '';
        return Material(
          color: Colors.white,
          borderRadius: BorderRadius.circular(24),
          child: InkWell(
            borderRadius: BorderRadius.circular(24),
            onTap: onOpen,
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Row(
                children: [
                  Container(
                    width: 48,
                    height: 48,
                    decoration: BoxDecoration(color: AppColours.errorBackground, borderRadius: BorderRadius.circular(14)),
                    child: const Icon(Icons.undo_rounded, color: AppColours.errorIcon),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          '${request.equipmentTitle} — ${request.clientName}',
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: AppTextStyles.cardTitle,
                        ),
                        const SizedBox(height: 2),
                        Text(
                          note.isEmpty ? t.returnedToInspector : t.returnedToInspectorNote(note),
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: AppTextStyles.caption.copyWith(height: 18 / 13),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}
