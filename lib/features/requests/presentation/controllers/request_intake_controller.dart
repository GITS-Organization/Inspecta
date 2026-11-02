import 'package:flutter/material.dart';

import '../../../clients/domain/entities/client.dart';
import '../../../clients/domain/usecases/get_client_machines.dart';
import '../../domain/entities/inspection_request.dart';
import '../../domain/entities/machine_suggestion.dart';
import '../../domain/entities/request_item.dart';
import '../../domain/usecases/complete_intake.dart';
import '../../domain/usecases/get_request_detail.dart';

/// One equipment row of the intake form. A row for an item the request already
/// has keeps that item's id; a new row has none, and the API gives it one when
/// the request is saved. A row linked to a client machine ([equipmentId]) took
/// the machine's details; the name, capacity, quantity and serial stay editable,
/// the make and model are the machine's own.
class IntakeItemRow {
  final RequestItem? original;
  final TextEditingController type;
  final TextEditingController capacity;
  final TextEditingController quantity;
  final TextEditingController serialNumber;

  String? equipmentId;
  String? category;
  String? manufacturer;
  String? model;

  IntakeItemRow.empty()
    : original = null,
      type = TextEditingController(),
      capacity = TextEditingController(),
      quantity = TextEditingController(text: '1'),
      serialNumber = TextEditingController();

  IntakeItemRow.fromItem(RequestItem item)
    : original = item,
      type = TextEditingController(text: item.type),
      capacity = TextEditingController(text: item.capacity),
      quantity = TextEditingController(text: '${item.quantity}'),
      serialNumber = TextEditingController(text: item.serialNumber),
      equipmentId = item.equipmentId,
      category = item.category,
      manufacturer = item.manufacturer,
      model = item.model;

  bool get linked => equipmentId != null;

  int get quantityValue {
    final value = int.tryParse(quantity.text.trim()) ?? 1;
    return value < 1 ? 1 : value;
  }

  /// "Make · model" of the linked machine, for the read-only line.
  String get machineDetails => [manufacturer, model]
      .whereType<String>()
      .map((part) => part.trim())
      .where((part) => part.isNotEmpty)
      .join(' · ');

  /// Takes the machine's details and links the row to it.
  void link(ClientMachine machine, {int? quantity}) {
    equipmentId = machine.id;
    type.text = machine.name;
    capacity.text = machine.capacity ?? '';
    serialNumber.text = machine.serialNumber ?? '';
    category = machine.category;
    manufacturer = machine.manufacturer;
    model = machine.model;
    if (quantity != null) this.quantity.text = '${quantity < 1 ? 1 : quantity}';
  }

  /// Drops the link (the machine belonged to another client); the typed
  /// details stay.
  void unlink() => equipmentId = null;

  /// The item this row stands for; null while it has no type.
  RequestItem? toItem() {
    final name = type.text.trim();
    if (name.isEmpty) return null;
    final capacityText = capacity.text.trim();
    final serial = serialNumber.text.trim();
    return RequestItem(
      // Empty for a new item: it has no id until the API saves it.
      id: original?.id ?? '',
      equipmentId: equipmentId,
      type: name,
      category: category,
      manufacturer: manufacturer,
      model: model,
      serialNumber: serial.isEmpty ? null : serial,
      prNumber: original?.prNumber,
      capacity: capacityText.isEmpty ? null : capacityText,
      quantity: quantityValue,
      location: original?.location,
    );
  }

  void dispose() {
    type.dispose();
    capacity.dispose();
    quantity.dispose();
    serialNumber.dispose();
  }
}

/// The state of the request screen's intake form — the client, the location,
/// the access notes and the equipment a new request needs before it can be
/// quoted (BR-02.5).
///
/// It starts from what the request already holds, and takes the request again
/// whenever it changes elsewhere ([applyRequest]: the email was auto-filled, the
/// form was saved), so what the coordinator has typed is never overwritten by a
/// value that didn't change on the server. Nothing reaches the API until [save].
class RequestIntakeController extends ChangeNotifier {
  final CompleteIntake _completeIntake;
  final GetRequestDetail _getRequest;
  final GetClientMachines _getMachines;

  InspectionRequest _request;
  String? _clientId;
  String _clientName;

  final TextEditingController location;
  final TextEditingController accessNotes;
  final List<IntakeItemRow> rows = [];

  /// The client's machines, loaded once per client.
  Future<List<ClientMachine>>? _machines;

  /// Whether Save was tried: only then do the missing parts turn red.
  bool submitted = false;
  bool saving = false;
  bool _disposed = false;

  RequestIntakeController({
    required InspectionRequest request,
    required CompleteIntake completeIntake,
    required GetRequestDetail getRequest,
    required GetClientMachines getMachines,
  }) : _request = request,
       _completeIntake = completeIntake,
       _getRequest = getRequest,
       _getMachines = getMachines,
       _clientId = request.clientId,
       _clientName = request.clientName,
       location = TextEditingController(text: request.location),
       accessNotes = TextEditingController(text: request.accessNotes) {
    location.addListener(notifyListeners);
    accessNotes.addListener(notifyListeners);
    _setRows(request.items);
  }

  String? get clientId => _clientId;
  String get clientName => _clientName;

  /// A request that already has a client keeps it; one that has none must be
  /// given one from the list.
  bool get canPickClient => _request.clientId == null;

  /// The ids of the machines already on the form.
  Set<String> get linkedEquipmentIds => {
    for (final row in rows)
      if (row.equipmentId != null) row.equipmentId!,
  };

  bool get hasClient => _clientId != null && _clientName.trim().isNotEmpty;
  bool get hasLocation => location.text.trim().isNotEmpty;
  bool get hasItems => rows.any((row) => row.toItem() != null);
  bool get isComplete => hasClient && hasLocation && hasItems;

  // ---- Client ----

  /// Picks [client] for a request that has none. Machines belong to one client,
  /// so those picked for another no longer fit.
  void setClient(Client client) {
    if (client.id != _clientId) {
      for (final row in rows) {
        row.unlink();
      }
      _machines = null;
    }
    _clientId = client.id;
    _clientName = client.name;
    if (!hasLocation && client.location != null) location.text = client.location!;
    notifyListeners();
  }

  // ---- Equipment ----

  /// The client's registered machines.
  Future<List<ClientMachine>> machines() {
    final clientId = _clientId;
    if (clientId == null) return Future.value(const []);
    return _machines ??= _loadMachines(clientId);
  }

  Future<List<ClientMachine>> _loadMachines(String clientId) async {
    try {
      return await _getMachines(clientId);
    } catch (_) {
      // Retry on the next ask rather than caching the failure.
      _machines = null;
      rethrow;
    }
  }

  void addRow() {
    _addRow(IntakeItemRow.empty());
    notifyListeners();
  }

  /// A new row for [machine], with its details.
  void addMachine(ClientMachine machine, {int quantity = 1}) {
    _addRow(IntakeItemRow.empty()..link(machine, quantity: quantity));
    notifyListeners();
  }

  /// Takes [machine]'s details into [row], linking it.
  void linkMachine(IntakeItemRow row, ClientMachine machine) {
    row.link(machine);
    notifyListeners();
  }

  /// Adds the machine [suggestion] points at. Returns false when it is already
  /// on the form.
  Future<bool> addSuggestion(MachineSuggestion suggestion) async {
    if (linkedEquipmentIds.contains(suggestion.equipmentId)) return false;
    ClientMachine? machine;
    try {
      for (final candidate in await machines()) {
        if (candidate.id == suggestion.equipmentId) machine = candidate;
      }
    } catch (_) {
      // The suggestion still names the machine; its details can be filled in.
    }
    if (_disposed) return false;
    final row = IntakeItemRow.empty();
    if (machine != null) {
      row.link(machine, quantity: suggestion.quantity);
    } else {
      row.equipmentId = suggestion.equipmentId;
      row.type.text = suggestion.name;
      row.quantity.text = '${suggestion.quantity < 1 ? 1 : suggestion.quantity}';
    }
    _addRow(row);
    notifyListeners();
    return true;
  }

  void removeRow(IntakeItemRow row) {
    if (!rows.remove(row)) return;
    _disposeLater([row]);
    notifyListeners();
  }

  // ---- Request ----

  /// Takes [request] — the same request after it changed on the server. Values
  /// the coordinator edited keep their edit; the equipment is the server's once
  /// it has any (its items now carry ids).
  void applyRequest(InspectionRequest request) {
    final old = _request;
    _request = request;
    if (request.clientId != null) {
      if (request.clientId != _clientId) _machines = null;
      _clientId = request.clientId;
      _clientName = request.clientName;
    }
    if (location.text.trim() == old.location.trim()) location.text = request.location;
    if (accessNotes.text.trim() == (old.accessNotes ?? '').trim()) {
      accessNotes.text = request.accessNotes ?? '';
    }
    if (request.items.isNotEmpty || old.items.isNotEmpty) _setRows(request.items);
    notifyListeners();
  }

  /// Saves the form, reads the request again and takes it. Returns the saved
  /// request, or null when something is still missing (the form then marks it).
  /// With [prepareQuotation] the same call opens the request's quotation.
  /// Throws what the API refused with.
  Future<InspectionRequest?> save({bool prepareQuotation = false}) async {
    submitted = true;
    notifyListeners();
    if (!isComplete || saving) return null;

    saving = true;
    notifyListeners();
    try {
      await _completeIntake(
        request: _request,
        clientName: _clientName,
        location: location.text,
        clientId: _clientId,
        accessNotes: accessNotes.text,
        items: [
          for (final row in rows)
            if (row.toItem() != null) row.toItem()!,
        ],
        prepareQuotation: prepareQuotation,
      );
      final fresh = await _getRequest(_request.id);
      if (fresh != null && !_disposed) applyRequest(fresh);
      return fresh ?? _request;
    } finally {
      saving = false;
      if (!_disposed) notifyListeners();
    }
  }

  // ---- Internals ----

  void _addRow(IntakeItemRow row) {
    for (final control in [row.type, row.capacity, row.quantity, row.serialNumber]) {
      control.addListener(notifyListeners);
    }
    rows.add(row);
  }

  void _setRows(List<RequestItem> items) {
    final old = List.of(rows);
    rows.clear();
    for (final item in items) {
      _addRow(IntakeItemRow.fromItem(item));
    }
    _disposeLater(old);
  }

  /// The fields of a removed row may still be on screen for the frame that
  /// removes them, so they are disposed after it.
  void _disposeLater(List<IntakeItemRow> old) {
    if (old.isEmpty) return;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      for (final row in old) {
        row.dispose();
      }
    });
  }

  @override
  void dispose() {
    _disposed = true;
    location.dispose();
    accessNotes.dispose();
    for (final row in rows) {
      row.dispose();
    }
    super.dispose();
  }
}
