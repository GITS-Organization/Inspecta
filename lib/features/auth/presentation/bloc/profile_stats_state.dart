part of 'profile_stats_cubit.dart';

class ProfileStatsState extends Equatable {
  // A coordinator's.
  final int? quotesSentThisMonth;
  final int? acceptedThisMonth;

  // A technical manager's.
  final int? reviewedThisMonth;
  final int? returnedThisMonth;
  final int? sentThisMonth;

  // An inspector's (and, in returnedThisMonth, shared with the manager's).
  final int? inspectionsThisMonth;
  final int? certificatesThisMonth;

  const ProfileStatsState({
    this.quotesSentThisMonth,
    this.acceptedThisMonth,
    this.reviewedThisMonth,
    this.returnedThisMonth,
    this.sentThisMonth,
    this.inspectionsThisMonth,
    this.certificatesThisMonth,
  });

  ProfileStatsState copyWith({
    int? quotesSentThisMonth,
    int? acceptedThisMonth,
    int? reviewedThisMonth,
    int? returnedThisMonth,
    int? sentThisMonth,
    int? inspectionsThisMonth,
    int? certificatesThisMonth,
  }) {
    return ProfileStatsState(
      quotesSentThisMonth: quotesSentThisMonth ?? this.quotesSentThisMonth,
      acceptedThisMonth: acceptedThisMonth ?? this.acceptedThisMonth,
      reviewedThisMonth: reviewedThisMonth ?? this.reviewedThisMonth,
      returnedThisMonth: returnedThisMonth ?? this.returnedThisMonth,
      sentThisMonth: sentThisMonth ?? this.sentThisMonth,
      inspectionsThisMonth: inspectionsThisMonth ?? this.inspectionsThisMonth,
      certificatesThisMonth: certificatesThisMonth ?? this.certificatesThisMonth,
    );
  }

  @override
  List<Object?> get props => [
    quotesSentThisMonth,
    acceptedThisMonth,
    reviewedThisMonth,
    returnedThisMonth,
    sentThisMonth,
    inspectionsThisMonth,
    certificatesThisMonth,
  ];
}
