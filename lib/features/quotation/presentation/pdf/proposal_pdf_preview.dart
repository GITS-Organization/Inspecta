import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/widgets/pdf_preview_page.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../auth/domain/entities/user_role.dart';
import '../../../auth/presentation/bloc/auth_cubit.dart';
import '../../../requests/domain/entities/inspection_request.dart';
import '../../domain/entities/quote_line_item.dart';
import 'proposal_pdf_builder.dart';

/// "INT/417/2026 rev01" — the proposal's reference. There's no sequential
/// numbering backend, so the serial is derived from the request's id the
/// way [certNumberFor] does for certificates: stable per request, not
/// globally sequential. [version] is the quotation's version, printed as
/// the revision.
String proposalNumberFor(InspectionRequest request, {int version = 1, DateTime? date}) {
  final year = (date ?? request.receivedAt).year;
  final serial = (request.id.hashCode.abs() % 1000).toString().padLeft(3, '0');
  return 'INT/$serial/$year rev${version.toString().padLeft(2, '0')}';
}

/// "QT-2026-0142" — the number on the proposal's cover, derived like
/// [proposalNumberFor] (stable per request, not globally sequential).
String quotationNumberFor(InspectionRequest request, {DateTime? date}) {
  final year = (date ?? request.receivedAt).year;
  final serial = (request.id.hashCode.abs() % 10000).toString().padLeft(4, '0');
  return 'QT-$year-$serial';
}

String _jobTitle(UserRole role) => switch (role) {
  UserRole.coordinator => 'Coordinator',
  UserRole.technicalManager => 'Technical Manager',
  UserRole.inspector => 'Inspector',
  UserRole.admin => 'Administrator',
  UserRole.finance => 'Finance',
  UserRole.managingDirector => 'Managing Director',
  UserRole.merchandiser => 'Merchandiser',
};

/// Opens the proposal in the full-screen preview. Emailing it to the client
/// isn't built yet (it needs a backend job), so for now it is view-only.
///
/// The offer is valid until [validUntil], or [validityDays] after it is
/// issued; it is prepared by whoever is signed in.
Future<void> previewProposalPdf(
  BuildContext context, {
  required InspectionRequest request,
  required List<QuoteLineItem> lines,
  required List<String> notes,
  List<String>? scopeOfWork,
  int version = 1,
  DateTime? date,
  DateTime? validUntil,
  int validityDays = 14,
}) {
  final t = AppLocalizations.of(context)!;
  final issued = date ?? DateTime.now();
  final number = proposalNumberFor(request, version: version, date: issued);
  final user = context.read<AuthCubit>().user;

  return PdfPreviewPage.open(
    context,
    title: t.proposalTitle,
    subtitle: number,
    fileName: '${number.replaceAll('/', '-').replaceAll(' ', '-')}.pdf',
    allowDownload: false,
    build: () async {
      return buildProposalPdf(
        request: request,
        lines: lines,
        notes: notes,
        scopeOfWork: scopeOfWork,
        details: ProposalDetails(
          proposalNumber: number,
          quotationNumber: quotationNumberFor(request, date: issued),
          date: issued,
          validUntil: validUntil ?? issued.add(Duration(days: validityDays)),
          preparedByName: user?.name ?? 'Integrity International for Inspection',
          preparedByTitle: user == null ? '' : _jobTitle(user.role),
        ),
      );
    },
  );
}
