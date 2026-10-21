import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/domain/entities/user_role.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/certificate_number.dart';
import '../../domain/entities/certificate.dart';
import 'certificate_pdf_builder.dart';

/// Opens the certificate's PDF in the full-screen preview. Used wherever the
/// app offers "View PDF" / "Preview".
///
/// Only a technical manager (who signs it) and a coordinator (who sends it on)
/// can download the PDF from there; everyone else
/// (inspectors included) gets a view-only preview.
Future<void> previewCertificatePdf(
  BuildContext context, {
  required InspectionRequest request,
  required Certificate certificate,
}) {
  final t = AppLocalizations.of(context)!;
  final number = certNumberFor(request);
  final role = context.read<AuthCubit>().user?.role;
  final canDownload = role == UserRole.technicalManager || role == UserRole.coordinator;
  return PdfPreviewPage.open(
    context,
    title: t.certificateTitle,
    subtitle: number,
    fileName: '$number.pdf',
    allowDownload: canDownload,
    build: () => buildCertificatePdf(request: request, certificate: certificate),
  );
}
