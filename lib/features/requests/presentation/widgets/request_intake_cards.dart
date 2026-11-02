import 'package:flutter/material.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../core/consts/app_text_styles.dart';
import '../../../../core/widgets/picker_sheet.dart';
import '../../../../injection.dart';
import '../../../../l10n/app_localizations.dart';
import '../../../clients/domain/entities/client.dart';
import '../../../clients/domain/usecases/get_clients.dart';
import '../controllers/request_intake_controller.dart';
import 'request_section_parts.dart';

/// The request's details as a form to check and edit: who the client is
/// (matched from the sender, or chosen from the team's clients) and where the
/// inspection takes place.
class RequestClientCard extends StatelessWidget {
  final RequestIntakeController intake;

  const RequestClientCard({super.key, required this.intake});

  Future<void> _pickClient(BuildContext context) async {
    final t = AppLocalizations.of(context)!;
    final client = await PickerSheet.show<Client>(
      context,
      title: t.chooseClientTitle,
      searchHint: t.chooseClientSearchHint,
      emptyText: t.noClientsFound,
      load: getIt<GetClients>().call,
      titleOf: (client) => client.name,
      subtitleOf: (client) => client.location,
    );
    if (client != null) intake.setClient(client);
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return ListenableBuilder(
      listenable: intake,
      builder: (context, _) {
        final hasClient = intake.hasClient;
        final clientError = intake.submitted && !hasClient;
        final locationError = intake.submitted && !intake.hasLocation;
        final pickable = intake.canPickClient;

        return RequestSection(
          padding: EdgeInsets.zero,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(18, 18, 18, 8),
                child: SectionTitle(t.requestDetailsTitle),
              ),
              Semantics(
                button: pickable,
                label: pickable ? t.chooseClientAction : null,
                child: DetailRow(
                  icon: Icons.apartment_rounded,
                  label: t.clientLabel,
                  error: clientError,
                  onTap: pickable ? () => _pickClient(context) : null,
                  value: Text(
                    hasClient ? intake.clientName : t.intakeClientHint,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: hasClient
                        ? AppTextStyles.cardTitle
                        : AppTextStyles.subtitle.copyWith(
                            color: clientError ? AppColours.errorText : null,
                          ),
                  ),
                  trailing: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      StatusPill(
                        label: hasClient ? t.clientMatchedBadge : t.clientRequiredBadge,
                        tone: hasClient
                            ? PillTone.success
                            : clientError
                            ? PillTone.danger
                            : PillTone.warning,
                        icon: hasClient ? Icons.check_rounded : null,
                      ),
                      if (pickable)
                        const Icon(Icons.chevron_right_rounded, size: 20, color: AppColours.inkMuted),
                    ],
                  ),
                ),
              ),
              const RowDivider(),
              _FieldRow(
                icon: Icons.place_outlined,
                label: t.siteLocationLabel,
                hint: t.locationHint,
                controller: intake.location,
                capitalization: TextCapitalization.words,
                errorText: locationError ? t.errorLocationRequired : null,
              ),
              const SizedBox(height: 6),
            ],
          ),
        );
      },
    );
  }
}

/// The request's equipment as one card per machine, each showing the
/// client's machine it is linked to (read-only; change or remove it to
/// change the item). Empty, it says how to fill it: from the email, or from
/// the client's machines.
class RequestEquipmentCard extends StatelessWidget {
  final RequestIntakeController intake;

  const RequestEquipmentCard({super.key, required this.intake});

  Future<void> _chooseMachine(BuildContext context, {IntakeItemRow? row}) async {
    final t = AppLocalizations.of(context)!;
    if (!intake.hasClient) return;
    final machine = await PickerSheet.show<ClientMachine>(
      context,
      title: t.machinePickerTitle,
      searchHint: t.searchMachinesHint,
      emptyText: t.noMachinesForClient,
      load: intake.machines,
      titleOf: (machine) => machine.name,
      // Capacity, make and model on one line, the serial on the next.
      subtitleOf: (machine) => [
        [machine.capacity, machine.manufacturer, machine.model]
            .whereType<String>()
            .map((part) => part.trim())
            .where((part) => part.isNotEmpty)
            .join(' · '),
        if (machine.serialNumber?.trim().isNotEmpty ?? false) t.machineSerialLine(machine.serialNumber!.trim()),
      ].where((line) => line.isNotEmpty).join('\n'),
      matches: (machine, query) => '${machine.name} ${machine.details}'.toLowerCase().contains(query),
      contextOf: (count) => '${intake.clientName} · ${t.machinesCount(count)}',
      itemIcon: Icons.precision_manufacturing_outlined,
      actionLabel: row == null ? t.suggestionAddAction : t.chooseMachineAction,
    );
    if (machine == null) return;
    if (row == null) {
      intake.addMachine(machine);
    } else {
      intake.linkMachine(row, machine);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return ListenableBuilder(
      listenable: intake,
      builder: (context, _) {
        final rows = intake.rows;
        final missing = intake.submitted && !intake.hasItems;

        return RequestSection(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Row(
                children: [
                  Expanded(child: SectionTitle(t.equipmentLabel)),
                  rows.isEmpty
                      ? StatusPill(
                          label: t.clientRequiredBadge,
                          tone: missing ? PillTone.danger : PillTone.info,
                        )
                      : StatusPill(label: t.itemsCount(rows.length), tone: PillTone.success),
                ],
              ),
              const SizedBox(height: 14),
              if (rows.isEmpty)
                _EmptyEquipment(error: missing)
              else
                for (var i = 0; i < rows.length; i++) ...[
                  if (i > 0) const SizedBox(height: 12),
                  _ItemEditor(
                    key: ObjectKey(rows[i]),
                    row: rows[i],
                    number: i + 1,
                    total: rows.length,
                    canChoose: intake.hasClient,
                    onChoose: () => _chooseMachine(context, row: rows[i]),
                    onRemove: () => intake.removeRow(rows[i]),
                  ),
                ],
              const SizedBox(height: 14),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: intake.hasClient ? () => _chooseMachine(context) : null,
                  icon: const Icon(Icons.format_list_bulleted_rounded, size: 18),
                  label: Text(t.clientMachinesAction, maxLines: 1, overflow: TextOverflow.ellipsis),
                  style: _outlinedStyle(),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

/// The bar under the form: what the request still needs, and Save.
class RequestIntakeFooter extends StatelessWidget {
  final RequestIntakeController intake;
  final Future<void> Function() onSave;

  const RequestIntakeFooter({super.key, required this.intake, required this.onSave});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return ListenableBuilder(
      listenable: intake,
      builder: (context, _) {
        final complete = intake.isComplete;
        final flagged = intake.submitted;
        final parts = [intake.hasClient, intake.hasLocation, intake.hasItems];
        return RequestFooterBar(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                complete ? t.intakeReadyTitle : t.intakeProgressTitle(parts.where((done) => done).length, parts.length),
                style: AppTextStyles.cardTitle.copyWith(fontSize: 14, height: 20 / 14),
              ),
              const SizedBox(height: 10),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: _ReadinessBar(label: t.clientLabel, done: parts[0], flagged: flagged)),
                  const SizedBox(width: 6),
                  Expanded(child: _ReadinessBar(label: t.locationLabel, done: parts[1], flagged: flagged)),
                  const SizedBox(width: 6),
                  Expanded(child: _ReadinessBar(label: t.equipmentLabel, done: parts[2], flagged: flagged)),
                ],
              ),
              const SizedBox(height: 14),
              RequestCtaButton(
                label: t.saveAndOpenQuotationAction,
                loading: intake.saving,
                ready: complete,
                onPressed: onSave,
              ),
            ],
          ),
        );
      },
    );
  }
}

// ---------------------------------------------------------------------------

ButtonStyle _outlinedStyle() => OutlinedButton.styleFrom(
  foregroundColor: AppColours.ink,
  side: const BorderSide(color: AppColours.border),
  textStyle: AppTextStyles.buttonLabel.copyWith(fontSize: 14, letterSpacing: 0, color: AppColours.ink),
  minimumSize: const Size(0, 48),
  padding: const EdgeInsets.symmetric(horizontal: 12),
  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
);

/// A detail the coordinator types in place: its label over the text, the way
/// the read-only rows show it, with a pencil where the chevron would be.
class _FieldRow extends StatefulWidget {
  final IconData icon;
  final String label;
  final String? hint;
  final TextEditingController controller;
  final TextCapitalization capitalization;
  final String? errorText;

  const _FieldRow({
    required this.icon,
    required this.label,
    required this.controller,
    this.hint,
    this.capitalization = TextCapitalization.none,
    this.errorText,
  });

  @override
  State<_FieldRow> createState() => _FieldRowState();
}

class _FieldRowState extends State<_FieldRow> {
  final _focus = FocusNode();

  @override
  void dispose() {
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final valueStyle = AppTextStyles.cardTitle;

    return DetailRow(
      icon: widget.icon,
      label: widget.label,
      error: widget.errorText != null,
      onTap: _focus.requestFocus,
      trailing: const Icon(Icons.edit_outlined, size: 18, color: AppColours.inkMuted),
      value: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          TextField(
            controller: widget.controller,
            focusNode: _focus,
            textCapitalization: widget.capitalization,
            style: valueStyle,
            cursorColor: AppColours.primaryDark,
            decoration: InputDecoration(
              isDense: true,
              border: InputBorder.none,
              contentPadding: const EdgeInsets.symmetric(vertical: 4),
              hintText: widget.hint,
              hintStyle: valueStyle.copyWith(
                fontWeight: FontWeight.w400,
                color: AppColours.inkMuted.withValues(alpha: 0.7),
              ),
            ),
          ),
          if (widget.errorText != null)
            Text(
              widget.errorText!,
              style: AppTextStyles.caption.copyWith(color: AppColours.errorText, fontWeight: FontWeight.w600),
            ),
        ],
      ),
    );
  }
}

/// Nothing added yet: a dashed box that says how to fill the list.
class _EmptyEquipment extends StatelessWidget {
  final bool error;

  const _EmptyEquipment({required this.error});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    return DashedBox(
      color: error ? AppColours.errorIcon : AppColours.border,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 18),
        child: SizedBox(
          width: double.infinity,
          child: Column(
            children: [
              Text(
                error ? t.errorEquipmentRequired : t.intakeEquipmentEmptyTitle,
                style: AppTextStyles.cardTitle.copyWith(color: error ? AppColours.errorText : null),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 4),
              Text(t.intakeEquipmentEmptyBody, style: AppTextStyles.caption, textAlign: TextAlign.center),
            ],
          ),
        ),
      ),
    );
  }
}

/// One machine of the request: the client's machine it is linked to and its
/// serial number. Both are read-only — a machine's details are the client's
/// own, so choosing another machine is how they change.
class _ItemEditor extends StatelessWidget {
  final IntakeItemRow row;
  final int number;
  final int total;
  final bool canChoose;
  final VoidCallback onChoose;
  final VoidCallback onRemove;

  const _ItemEditor({
    super.key,
    required this.row,
    required this.number,
    required this.total,
    required this.canChoose,
    required this.onChoose,
    required this.onRemove,
  });

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;

    return ListenableBuilder(
      listenable: Listenable.merge([row.type, row.serialNumber]),
      builder: (context, _) {
        final linked = row.linked;
        final title = row.type.text.trim();

        return Container(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(18),
            border: Border.all(color: AppColours.border),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsetsDirectional.fromSTEB(14, 14, 6, 12),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const IconTile(icon: Icons.precision_manufacturing_outlined, size: 44),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(t.equipmentItemOf(number, total), style: rowLabelStyle),
                            Text(
                              title.isEmpty ? t.equipmentTypeLabel : title,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: AppTextStyles.cardTitle.copyWith(
                                fontSize: 16,
                                height: 21 / 16,
                                color: title.isEmpty ? AppColours.inkMuted : null,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                    // Apart from "Change", and tinted as a danger.
                    Padding(
                      padding: const EdgeInsetsDirectional.only(end: 8),
                      child: IconButton(
                        onPressed: onRemove,
                        tooltip: t.removeItemAction,
                        style: IconButton.styleFrom(
                          backgroundColor: AppColours.errorBackground,
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                        ),
                        constraints: const BoxConstraints.tightFor(width: 44, height: 44),
                        icon: const Icon(Icons.delete_outline_rounded, size: 22, color: AppColours.errorText),
                      ),
                    ),
                  ],
                ),
              ),
              // Linked to a registered machine, or free text.
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 14),
                child: MachineLinkStrip(
                  linked: linked,
                  label: linked ? t.clientMachineLinked : t.machineNotLinked,
                  details: row.machineDetails,
                  action: canChoose
                      ? UnderlineLink(
                          label: linked ? t.changeMachineAction : t.chooseMachineAction,
                          onPressed: onChoose,
                          fontSize: 13,
                          minHeight: 36,
                        )
                      : null,
                ),
              ),
              Padding(
                padding: const EdgeInsets.all(14),
                child: IntakeField(
                  label: t.serialNumberLabel,
                  hint: t.notSet,
                  controller: row.serialNumber,
                  readOnly: true,
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

/// One of the three things a request needs before it can be quoted: a bar
/// under its name, green once it is there.
class _ReadinessBar extends StatelessWidget {
  final String label;
  final bool done;

  /// Whether Save was tried: a missing part is then drawn as a problem rather
  /// than as something still to do.
  final bool flagged;

  const _ReadinessBar({required this.label, required this.done, required this.flagged});

  @override
  Widget build(BuildContext context) {
    final t = AppLocalizations.of(context)!;
    final problem = !done && flagged;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          height: 4,
          decoration: BoxDecoration(
            color: done
                ? AppColours.successIcon
                : problem
                ? AppColours.errorIcon
                : AppColours.border,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
        const SizedBox(height: 6),
        Text(
          done ? label : t.partNeeded(label),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: AppTextStyles.badge.copyWith(
            fontWeight: done ? FontWeight.w500 : FontWeight.w600,
            color: done
                ? AppColours.successText
                : problem
                ? AppColours.errorText
                : AppColours.ink,
          ),
        ),
      ],
    );
  }
}

/// A labelled text field in the form's style.
class IntakeField extends StatelessWidget {
  final String label;
  final TextEditingController controller;
  final String? hint;
  final IconData? icon;
  final String? errorText;
  final int maxLines;
  final TextCapitalization capitalization;

  /// Shows the value without letting it be changed (it can still be selected
  /// and copied); focusing it doesn't light up the border.
  final bool readOnly;

  const IntakeField({
    super.key,
    required this.label,
    required this.controller,
    this.hint,
    this.icon,
    this.errorText,
    this.maxLines = 1,
    this.capitalization = TextCapitalization.none,
    this.readOnly = false,
  });

  @override
  Widget build(BuildContext context) {
    final idleBorder = OutlineInputBorder(
      borderRadius: BorderRadius.circular(12),
      borderSide: const BorderSide(color: AppColours.border),
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: AppTextStyles.fieldLabel.copyWith(fontSize: 13)),
        const SizedBox(height: 6),
        TextField(
          controller: controller,
          readOnly: readOnly,
          minLines: 1,
          maxLines: maxLines,
          textCapitalization: capitalization,
          style: AppTextStyles.input.copyWith(fontWeight: FontWeight.w400, fontSize: 15),
          decoration: InputDecoration(
            isDense: true,
            hintText: hint,
            hintStyle: AppTextStyles.input.copyWith(
              fontWeight: FontWeight.w400,
              fontSize: 15,
              color: AppColours.inkMuted.withValues(alpha: 0.7),
            ),
            prefixIcon: icon == null ? null : Icon(icon, size: 20, color: AppColours.inkMuted),
            contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
            filled: true,
            fillColor: AppColours.surfaceMuted,
            errorText: errorText,
            enabledBorder: idleBorder,
            focusedBorder: readOnly
                ? idleBorder
                : OutlineInputBorder(
                    borderRadius: BorderRadius.circular(12),
                    borderSide: const BorderSide(color: AppColours.primaryDark, width: 1.5),
                  ),
            errorBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: AppColours.errorIcon, width: 1.5),
            ),
            focusedErrorBorder: OutlineInputBorder(
              borderRadius: BorderRadius.circular(12),
              borderSide: const BorderSide(color: AppColours.errorIcon, width: 1.5),
            ),
          ),
        ),
      ],
    );
  }
}
