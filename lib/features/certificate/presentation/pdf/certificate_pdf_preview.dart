import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/domain/entities/user_role.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../machines/domain/usecases/get_machine_pdf.dart';
import '../../domain/certificate_number.dart';
import '../bloc/certificate_cubit.dart';

/// Opens the machine's certificate in the full-screen preview. Used by the
/// certificate form's "Preview".
///
/// The PDF is the one the API renders (marked DRAFT until the technical
/// manager approves it), the same file the dashboard shows — never one built
/// on the device, so what the inspector previews is what the manager reviews.
/// What was typed is saved first, so the PDF reflects the screen.
///
/// Only a technical manager (who signs it) and a coordinator (who sends it on)
/// can download the PDF from there; everyone else
/// (inspectors included) gets a view-only preview.
Future<void> previewCertificatePdf(
  BuildContext context, {
  required CertificateCubit cubit,
}) {
  final t = AppLocalizations.of(context)!;
  final request = cubit.request;
  final number = certNumberFor(request);
  final role = context.read<AuthCubit>().user?.role;
  final canDownload = role == UserRole.technicalManager || role == UserRole.coordinator;
  return PdfPreviewPage.open(
    context,
    title: t.certificateTitle,
    subtitle: number,
    fileName: '$number.pdf',
    allowDownload: canDownload,
    build: () async {
      // Runs behind the preview's "Preparing PDF…" spinner, and again on Retry.
      await cubit.persist();
      return getIt<GetMachinePdf>()(request.id, cubit.requestItemId);
    },
  );
}
