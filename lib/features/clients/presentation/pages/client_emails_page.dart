import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:skeletonizer/skeletonizer.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/framework/mtoast.dart';
import '../../../../core/shared/m_card.dart';
import '../../../../core/shared/m_notice.dart';
import '../../../../core/shared/m_text_field.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../machines/presentation/widgets/machine_widgets.dart';
import '../../domain/entities/client.dart';
import '../bloc/client_emails_cubit.dart';
import '../widgets/clients_labels.dart';

/// A client's "Emails" screen: the addresses and domains whose emails become
/// the client's requests. Adding one also moves the waiting "maybe" emails
/// from it onto the client.
///
/// [suggestedSender] (the sender of a "maybe" email being handled) starts the
/// add field off, with the choice of the address itself or its whole domain.
///
/// Pops with how many waiting emails became requests while the screen was
/// open, so the inbox that opened it can reload.
class ClientEmailsPage extends StatelessWidget {
  final Client client;
  final String? suggestedSender;

  const ClientEmailsPage({super.key, required this.client, this.suggestedSender});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<ClientEmailsCubit>(param1: client)..start(),
      child: AnnotatedRegion<SystemUiOverlayStyle>(
        value: SystemUiOverlayStyle.dark,
        child: _View(client: client, suggestedSender: suggestedSender),
      ),
    );
  }
}

class _View extends StatefulWidget {
  final Client client;
  final String? suggestedSender;

  const _View({required this.client, required this.suggestedSender});

  @override
  State<_View> createState() => _ViewState();
}

class _ViewState extends State<_View> {
  late final _input = TextEditingController(text: widget.suggestedSender ?? '');

  @override
  void dispose() {
    _input.dispose();
    super.dispose();
  }

  /// The domain of the suggested sender (`alnoor.com` of `buyer@alnoor.com`).
  String? get _suggestedDomain {
    final sender = widget.suggestedSender;
    final at = sender?.indexOf('@') ?? -1;
    return sender == null || at < 0 || at == sender.length - 1 ? null : sender.substring(at + 1);
  }

  Future<void> _add() async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<ClientEmailsCubit>();
    final pattern = _input.text.trim();
    if (pattern.isEmpty) return;

    final added = await cubit.add(pattern);
    if (!mounted) return;
    if (added == null) {
      MToast.showError(message: cubit.state.actionFailure!.message(t));
      return;
    }
    _input.clear();
    MToast.showSuccess(
      message: added.promotedRequests > 0
          ? t.clientEmailPromotedMessage(added.promotedRequests)
          : t.clientEmailAdded,
    );
  }

  Future<void> _remove(ClientEmail email) async {
    final t = AppLocalizations.of(context)!;
    final cubit = context.read<ClientEmailsCubit>();
    final removed = await cubit.remove(email);
    if (!removed && mounted) MToast.showError(message: cubit.state.actionFailure!.message(t));
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final domain = _suggestedDomain;

    return PopScope(
      canPop: false,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) Navigator.of(context).pop(context.read<ClientEmailsCubit>().state.promoted);
      },
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            MachinesHeader(eyebrow: widget.client.name, title: t.clientEmailsTitle),
            const SizedBox(height: 12),
            Expanded(
              child: BlocBuilder<ClientEmailsCubit, ClientEmailsState>(
                builder: (context, state) {
                  if (state.status == ClientEmailsStatus.error) {
                    return Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            MNotice(type: MNoticeType.error, message: state.failure!.message(t)),
                            const SizedBox(height: 16),
                            OutlinedButton(onPressed: context.read<ClientEmailsCubit>().retry, child: Text(t.retry)),
                          ],
                        ),
                      ),
                    );
                  }

                  final loading = state.status == ClientEmailsStatus.loading;
                  return Skeletonizer(
                    enabled: loading,
                    child: ListView(
                      physics: loading ? const NeverScrollableScrollPhysics() : null,
                      keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
                      padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
                      children: [
                        MNotice(type: MNoticeType.info, message: t.clientEmailsIntro),
                        const SizedBox(height: 12),
                        MCard(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              MTextField(
                                controller: _input,
                                enabled: !loading && !state.isAdding,
                                keyboardType: TextInputType.emailAddress,
                                textInputAction: TextInputAction.done,
                                prefixIcon: Icons.alternate_email_rounded,
                                hintText: t.clientEmailAddHint,
                                onSubmitted: (_) => _add(),
                                trailing: state.isAdding
                                    ? const Padding(
                                        padding: EdgeInsets.all(10),
                                        child: SizedBox.square(
                                          dimension: 20,
                                          child: CircularProgressIndicator(strokeWidth: 2.5),
                                        ),
                                      )
                                    : IconButton(
                                        onPressed: loading ? null : _add,
                                        iconSize: 22,
                                        color: AppColours.primaryColor,
                                        icon: const Icon(Icons.add_rounded),
                                        tooltip: t.clientEmailAddAction,
                                      ),
                              ),
                              if (domain != null) ...[
                                const SizedBox(height: 10),
                                Wrap(
                                  spacing: 8,
                                  children: [
                                    ActionChip(
                                      label: Text(t.clientEmailThisAddress, style: AppTextStyles.caption),
                                      onPressed: () => setState(() => _input.text = widget.suggestedSender!),
                                      backgroundColor: AppColours.surfaceMuted,
                                      side: BorderSide.none,
                                    ),
                                    ActionChip(
                                      label: Text(t.clientEmailWholeDomain(domain), style: AppTextStyles.caption),
                                      onPressed: () => setState(() => _input.text = domain),
                                      backgroundColor: AppColours.surfaceMuted,
                                      side: BorderSide.none,
                                    ),
                                  ],
                                ),
                              ],
                            ],
                          ),
                        ),
                        const SizedBox(height: 16),
                        if (!loading && state.emails.isEmpty)
                          Padding(
                            padding: const EdgeInsets.symmetric(vertical: 24),
                            child: Center(child: Text(t.clientEmailsEmpty, style: AppTextStyles.subtitle)),
                          )
                        else
                          MCard(
                            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
                            child: Column(
                              children: [
                                for (final (index, email) in (loading
                                        ? const [ClientEmail(id: 'a', pattern: 'buyer@company.com')]
                                        : state.emails)
                                    .indexed) ...[
                                  if (index > 0) const Divider(height: 1, color: AppColours.borderSubtle),
                                  _EmailRow(
                                    email: email,
                                    removing: state.removing.contains(email.id),
                                    onRemove: () => _remove(email),
                                  ),
                                ],
                              ],
                            ),
                          ),
                      ],
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// One registered address or domain, with a remove button.
class _EmailRow extends StatelessWidget {
  final ClientEmail email;
  final bool removing;
  final VoidCallback onRemove;

  const _EmailRow({required this.email, required this.removing, required this.onRemove});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Icon(
            email.isDomain ? Icons.domain_rounded : Icons.mail_outline_rounded,
            size: 20,
            color: AppColours.inkMuted,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              email.pattern,
              style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
              overflow: TextOverflow.ellipsis,
            ),
          ),
          if (email.isDomain) ...[
            const SizedBox(width: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(
                color: AppColours.chipBlueBackground,
                borderRadius: BorderRadius.circular(999),
              ),
              child: Text(
                t.clientEmailDomainLabel,
                style: AppTextStyles.badge.copyWith(color: AppColours.chipBlueText),
              ),
            ),
          ],
          IconButton(
            onPressed: removing ? null : onRemove,
            tooltip: t.removeItemAction,
            icon: removing
                ? const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Icon(Icons.delete_outline_rounded, size: 20, color: AppColours.errorIcon),
          ),
        ],
      ),
    );
  }
}
