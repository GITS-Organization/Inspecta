import 'package:flutter/material.dart';

import '../../../auth/presentation/widgets/user_app_header.dart';

/// The technical manager tabs' header: the shared banner (avatar that opens
/// Profile, role and greeting, bell, [title]) with an optional [child]
/// beneath — the counts on the Review tab.
class ReviewHeader extends StatelessWidget {
  final String title;
  final VoidCallback onOpenProfile;
  final Widget? child;

  const ReviewHeader({super.key, required this.title, required this.onOpenProfile, this.child});

  @override
  Widget build(BuildContext context) {
    return UserAppHeader(title: title, onOpenProfile: onOpenProfile, bottom: child);
  }
}
