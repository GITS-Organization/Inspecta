import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/consts/app_colors.dart';
import '../../../../injection.dart';
import '../../domain/entities/inspection_request.dart';
import '../bloc/requests_list_cubit.dart';
import '../widgets/requests_inbox_header.dart';
import '../widgets/requests_list_view.dart';
import 'request_detail_page.dart';

/// Feature 02's "Requests inbox": the header, search, tabs, and the list
/// itself. Content only — the bottom nav belongs to whichever role shell
/// hosts this screen (see `CoordinatorRootPage`).
class RequestsInboxPage extends StatelessWidget {
  final VoidCallback onOpenProfile;

  const RequestsInboxPage({super.key, required this.onOpenProfile});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => getIt<RequestsListCubit>()..start(),
      child: _RequestsInboxView(onOpenProfile: onOpenProfile),
    );
  }
}

class _RequestsInboxView extends StatefulWidget {
  final VoidCallback onOpenProfile;

  const _RequestsInboxView({required this.onOpenProfile});

  @override
  State<_RequestsInboxView> createState() => _RequestsInboxViewState();
}

class _RequestsInboxViewState extends State<_RequestsInboxView> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _openDetail(BuildContext context, InspectionRequest request) async {
    final cubit = context.read<RequestsListCubit>();
    final changed = await Navigator.of(context).push<bool>(
      MaterialPageRoute<bool>(
        builder: (_) => RequestDetailPage(request: request),
      ),
    );
    // The list isn't live: reload it when the request was completed.
    if (changed == true) cubit.refresh();
  }

  @override
  Widget build(BuildContext context) {
    return AnnotatedRegion<SystemUiOverlayStyle>(
      value: SystemUiOverlayStyle.dark,
      child: Scaffold(
        backgroundColor: AppColours.background,
        body: Column(
          children: [
            RequestsInboxHeader(
              searchController: _searchController,
              onOpenProfile: widget.onOpenProfile,
            ),
            Expanded(child: RequestsListView(onOpen: _openDetail)),
          ],
        ),
      ),
    );
  }
}
