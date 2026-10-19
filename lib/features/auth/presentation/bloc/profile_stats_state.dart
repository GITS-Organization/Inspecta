part of 'profile_stats_cubit.dart';

class ProfileStatsState extends Equatable {
  // A coordinator's.
  final int? quotesSentThisMonth;
  final int? acceptedThisMonth;
  final int? clientsCount;

  // A technical manager's.
  final int? reviewedThisMonth;
  final int? returnedThisMonth;
  final int? sentThisMonth;

  const ProfileStatsState({
    this.quotesSentThisMonth,
    this.acceptedThisMonth,
    this.clientsCount,
    this.reviewedThisMonth,
    this.returnedThisMonth,
    this.sentThisMonth,
  });

  ProfileStatsState copyWith({
    int? quotesSentThisMonth,
    int? acceptedThisMonth,
    int? clientsCount,
    int? reviewedThisMonth,
    int? returnedThisMonth,
    int? sentThisMonth,
  }) {
    return ProfileStatsState(
      quotesSentThisMonth: quotesSentThisMonth ?? this.quotesSentThisMonth,
      acceptedThisMonth: acceptedThisMonth ?? this.acceptedThisMonth,
      clientsCount: clientsCount ?? this.clientsCount,
      reviewedThisMonth: reviewedThisMonth ?? this.reviewedThisMonth,
      returnedThisMonth: returnedThisMonth ?? this.returnedThisMonth,
      sentThisMonth: sentThisMonth ?? this.sentThisMonth,
    );
  }

  @override
  List<Object?> get props => [
    quotesSentThisMonth,
    acceptedThisMonth,
    clientsCount,
    reviewedThisMonth,
    returnedThisMonth,
    sentThisMonth,
  ];
}
