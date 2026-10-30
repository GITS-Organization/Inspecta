// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get showPassword => 'Show password';

  @override
  String get hidePassword => 'Hide password';

  @override
  String get back => 'Back';

  @override
  String get cancel => 'Cancel';

  @override
  String get splashStart => 'Get started';

  @override
  String get splashWelcome => 'Welcome to';

  @override
  String get splashHint =>
      'Digital inspections, certificates and requests — all in one place.';

  @override
  String get loginHeroEyebrow => 'INSPECTION MANAGEMENT';

  @override
  String get loginHeroLine1 => 'Inspect smarter.';

  @override
  String get loginHeroLine2 => 'Certify faster.';

  @override
  String get signInTitle => 'Welcome back';

  @override
  String get signInSubtitle =>
      'Sign in to see your requests, tasks and certificates.';

  @override
  String get workEmail => 'Work email';

  @override
  String get emailHint => 'name@company.com';

  @override
  String get password => 'Password';

  @override
  String get keepMeSignedIn => 'Keep me signed in';

  @override
  String get forgotPassword => 'Forgot password?';

  @override
  String get signIn => 'Sign in';

  @override
  String get signInRoleHint =>
      'You\'ll go straight to your own screens — coordinator, inspector or technical manager.';

  @override
  String get noAccount => 'No account? Ask your admin to add you.';

  @override
  String get resetTitle => 'Reset your password';

  @override
  String get resetSubtitle =>
      'Enter your work email and we\'ll send you a link to set a new password.';

  @override
  String get sendResetLink => 'Send reset link';

  @override
  String get resendResetLink => 'Send again';

  @override
  String get checkInboxTitle => 'Check your inbox';

  @override
  String get checkInboxMessage =>
      'If this email has an account, a reset link is on its way. It expires in 1 hour.';

  @override
  String get rememberedIt => 'Remembered it?';

  @override
  String get backToSignIn => 'Back to sign in';

  @override
  String get errorEmailRequired => 'Enter your work email.';

  @override
  String get errorEmailInvalid => 'Enter a valid email address.';

  @override
  String get errorPasswordRequired => 'Enter your password.';

  @override
  String get errorInvalidCredentials => 'Email or password is incorrect.';

  @override
  String get errorAccountDisabled =>
      'This account is deactivated. Contact your admin.';

  @override
  String get errorNoProfile =>
      'Your account isn\'t set up yet. Ask your admin to give you a role.';

  @override
  String get errorTooManyAttempts =>
      'Too many failed attempts. Wait a few minutes and try again.';

  @override
  String get errorNetwork =>
      'No connection. Check your internet and try again.';

  @override
  String get errorUnavailable =>
      'Sign-in isn\'t available right now. Try again later.';

  @override
  String get errorUnknown => 'Something went wrong. Try again.';

  @override
  String get profile => 'Profile';

  @override
  String get email => 'Email';

  @override
  String get phone => 'Phone';

  @override
  String get qualifications => 'Qualifications';

  @override
  String get notSet => 'Not set';

  @override
  String get signOut => 'Sign out';

  @override
  String get signOutConfirmTitle => 'Sign out?';

  @override
  String get signOutConfirmMessage =>
      'You\'ll need your email and password to sign in again.';

  @override
  String get profileThisMonth => 'This month';

  @override
  String get profileStatQuotesSent => 'Quotes sent';

  @override
  String get profileStatAccepted => 'Accepted';

  @override
  String get profileStatClients => 'Clients';

  @override
  String get profileStatJobsAssigned => 'Jobs assigned';

  @override
  String get profileStatReassigned => 'Reassigned';

  @override
  String get profileStatInspectors => 'Inspectors';

  @override
  String get profileStatInspections => 'Inspections';

  @override
  String get profileStatCertificates => 'Certificates';

  @override
  String get profileStatReturned => 'Returned';

  @override
  String get profileStatReviewed => 'Reviewed';

  @override
  String get profileStatSent => 'Sent';

  @override
  String get editAction => 'Edit';

  @override
  String get changePhotoAction => 'Change photo';

  @override
  String get viewAction => 'View';

  @override
  String get myWorkTitle => 'My work';

  @override
  String get qualifiedForLabel => 'Qualified for';

  @override
  String get availableForTasksLabel => 'Available for new tasks';

  @override
  String get requestLeaveAction => 'Request leave';

  @override
  String get mySignatureTitle => 'My signature';

  @override
  String get updateAction => 'Update';

  @override
  String get signatureOnFileLabel => 'Signature on file';

  @override
  String get noSignatureLabel => 'No signature added yet';

  @override
  String get accountSectionTitle => 'Account';

  @override
  String get personalInfoLabel => 'Personal info';

  @override
  String get changePasswordLabel => 'Change password';

  @override
  String get languageLabel => 'Language';

  @override
  String get helpGuidesLabel => 'Help & guides';

  @override
  String get contactAdminLabel => 'Contact admin';

  @override
  String versionLabel(String version) {
    return 'Version $version';
  }

  @override
  String get roleCoordinator => 'Coordinator';

  @override
  String get roleInspector => 'Inspector';

  @override
  String get roleTechnicalManager => 'Technical manager';

  @override
  String get roleAdmin => 'Admin';

  @override
  String get homeCoordinator => 'Ready to assign';

  @override
  String get homeInspector => 'My tasks';

  @override
  String get homeTechnicalManager => 'Review queue';

  @override
  String get homeAdmin => 'Users';

  @override
  String homeGreeting(String name) {
    return 'Hello, $name';
  }

  @override
  String get homeComingSoon =>
      'You\'re signed in and on the right screen. This list is built in a later feature.';

  @override
  String get justNow => 'Just now';

  @override
  String minutesAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count m ago',
      one: '1 m ago',
    );
    return '$_temp0';
  }

  @override
  String hoursAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count h ago',
      one: '1 h ago',
    );
    return '$_temp0';
  }

  @override
  String daysAgo(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count d ago',
      one: '1 d ago',
    );
    return '$_temp0';
  }

  @override
  String get statusRequestReceived => 'New';

  @override
  String get statusQuoteDraft => 'Draft';

  @override
  String get statusQuoteSent => 'Sent';

  @override
  String get statusClientCountered => 'Countered';

  @override
  String get statusQuoteRejected => 'Rejected';

  @override
  String get statusClientDeclined => 'Declined';

  @override
  String get statusQuoteAccepted => 'Accepted';

  @override
  String get statusAssigned => 'Assigned';

  @override
  String get statusTaskAccepted => 'Task accepted';

  @override
  String get statusInProgress => 'In progress';

  @override
  String get statusCertificateSubmitted => 'Certificate submitted';

  @override
  String get statusCertificateReturned => 'Returned';

  @override
  String get statusCertificateApproved => 'Approved';

  @override
  String get statusSentToClient => 'Sent to client';

  @override
  String get waitingOver24h => 'Waiting 24h+';

  @override
  String get requestsGreetingMorning => 'Good morning';

  @override
  String get requestsGreetingAfternoon => 'Good afternoon';

  @override
  String get requestsGreetingEvening => 'Good evening';

  @override
  String requestsGreetingNameRole(String name, String role) {
    return '$name · $role';
  }

  @override
  String get notifications => 'Notifications';

  @override
  String get navRequests => 'Requests';

  @override
  String get navQuotations => 'Quotations';

  @override
  String get searchRequestsHint => 'Search client, equipment or ID';

  @override
  String get tabNew => 'New';

  @override
  String tabCount(String label, int count) {
    return '$label · $count';
  }

  @override
  String get needsYourReply => 'Needs your reply';

  @override
  String get newestFirst => 'Newest first';

  @override
  String get reviewAndQuote => 'Review & quote';

  @override
  String get continueQuote => 'Continue quote';

  @override
  String get newClientTag => 'New client';

  @override
  String unitsCount(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: '$count units',
      one: '1 unit',
    );
    return '$_temp0';
  }

  @override
  String get emptyNewRequests => 'No new requests. Nice and clear.';

  @override
  String get tabMaybe => 'Maybe';

  @override
  String get maybeRequestsHeading => 'From unknown senders';

  @override
  String get emptyMaybeRequests => 'No emails from unknown senders.';

  @override
  String get confirmRequest => 'Confirm as request';

  @override
  String get confirmRequestError =>
      'Couldn\'t confirm the request. Please try again.';

  @override
  String get deleteRequest => 'Delete';

  @override
  String get deleteRequestTitle => 'Delete this email?';

  @override
  String get deleteRequestMessage =>
      'It will be removed from your inbox and cannot be restored.';

  @override
  String get deleteRequestError =>
      'Couldn\'t delete the request. Please try again.';

  @override
  String emptySearch(String query) {
    return 'Nothing matches \"$query\".';
  }

  @override
  String get requestsLoadError => 'Couldn\'t load the requests inbox.';

  @override
  String get requestsConflict =>
      'This request was changed by someone else. Pull down to refresh the inbox, then open it again.';

  @override
  String get requestsPermissionDenied =>
      'You don\'t have permission to view the requests inbox.';

  @override
  String get retry => 'Retry';

  @override
  String get requestDetailTitle => 'Request';

  @override
  String get stepRequest => 'Request';

  @override
  String get stepQuote => 'Quote';

  @override
  String get stepAssign => 'Assign';

  @override
  String get stepInspect => 'Inspect';

  @override
  String get stepCertify => 'Certify';

  @override
  String get stepSent => 'Sent';

  @override
  String get clientLabel => 'Client';

  @override
  String get locationLabel => 'Location';

  @override
  String get clientLocationLabel => 'Client location';

  @override
  String get equipmentLocationLabel => 'Equipment location';

  @override
  String equipmentItemOf(int number, int total) {
    return 'Item $number of $total';
  }

  @override
  String get prNumberLabel => 'PR number';

  @override
  String get preferredDateLabel => 'Preferred date';

  @override
  String get accessNotesLabel => 'Access notes';

  @override
  String get equipmentLabel => 'Equipment';

  @override
  String get capacityLabel => 'Capacity';

  @override
  String get quantityLabel => 'Quantity';

  @override
  String get readFullEmail => 'Read full email';

  @override
  String get showLessEmail => 'Show less';

  @override
  String get notReadyToQuote =>
      'Add the client, at least one equipment item and the location before quoting.';

  @override
  String get completeRequestAction => 'Complete request';

  @override
  String get completeRequestSheetTitle => 'Complete this request';

  @override
  String get completeRequestSheetSubtitle =>
      'Add the client, location and equipment this request needs before it can be quoted.';

  @override
  String get clientNameLabel => 'Client name';

  @override
  String get errorClientNameRequired => 'Enter the client\'s name.';

  @override
  String get addEquipmentItemAction => '+ Add equipment';

  @override
  String get removeItemAction => 'Remove';

  @override
  String get equipmentTypeLabel => 'Equipment type';

  @override
  String get saveAction => 'Save';

  @override
  String get errorEquipmentTypeRequired => 'Enter the equipment type.';

  @override
  String get errorEquipmentRequired => 'Add at least one equipment item.';

  @override
  String get requestCompleted => 'Request completed.';

  @override
  String requestMovedOnMessage(String status) {
    return 'A quote already exists for this request — current status: $status. Manage it from Quotations.';
  }

  @override
  String requestRejectedReasonMessage(String reason) {
    return 'You rejected this request: $reason';
  }

  @override
  String get viewInQuotationsAction => 'View in Quotations';

  @override
  String get comingSoon => 'This isn\'t built yet.';

  @override
  String get howDoYouWantToReply => 'How do you want to reply?';

  @override
  String get replyAccept => 'Accept';

  @override
  String get replyAcceptSubtitle => 'Use your standard rate';

  @override
  String get replyOffer => 'Send price offer';

  @override
  String get replyOfferSubtitle => 'Set a price for this request';

  @override
  String get replyReject => 'Reject';

  @override
  String get replyRejectSubtitle => 'Tell the client why';

  @override
  String get chooseReplyTypeFirst => 'Choose how you want to reply first.';

  @override
  String get pricePerUnit => 'Price per unit';

  @override
  String get validFor => 'Valid for';

  @override
  String validityDaysOption(int days) {
    return '$days days';
  }

  @override
  String get messageToClient => 'Message to client';

  @override
  String get optionalLabel => 'optional';

  @override
  String get reasonToClient => 'Reason for rejecting';

  @override
  String totalForUnits(String units) {
    return 'Total for $units';
  }

  @override
  String get standardRateApplies => 'Uses your standard rate';

  @override
  String get saveDraft => 'Save draft';

  @override
  String get sendQuotation => 'Send quotation';

  @override
  String get errorPriceRequired => 'Enter a price per unit.';

  @override
  String get errorScopeRequired =>
      'Add at least one point to the scope of work.';

  @override
  String get errorReasonRequired => 'Tell the client why you\'re rejecting.';

  @override
  String get actionPermissionDenied => 'You don\'t have permission to do that.';

  @override
  String get actionFailed => 'Something went wrong. Try again.';

  @override
  String get draftSaved => 'Draft saved.';

  @override
  String quotationSent(int version) {
    return 'Quotation sent (v$version).';
  }

  @override
  String get requestRejected => 'Request rejected.';

  @override
  String get quotationsTitle => 'Quotations';

  @override
  String get filter => 'Filter';

  @override
  String get quotationsEmpty => 'No quotations yet.';

  @override
  String quoteVersionLabel(int version) {
    return 'v$version';
  }

  @override
  String get quoteDraftLabel => 'Draft';

  @override
  String get quotationExpiredLabel => 'Expired';

  @override
  String get quotationSupersededLabel => 'Superseded';

  @override
  String get requestNoLongerAvailable => 'This request is no longer available.';

  @override
  String quoteNumberVersion(String quoteNumber, String version) {
    return '$quoteNumber · $version';
  }

  @override
  String get tabOpen => 'Open';

  @override
  String get tabAccepted => 'Accepted';

  @override
  String get tabRejected => 'Rejected';

  @override
  String get tileAwaitingClient => 'Awaiting client';

  @override
  String get tileClientReplied => 'Client replied';

  @override
  String get tileExpiringSoon => 'Expiring soon';

  @override
  String get tileAcceptedThisMonth => 'Accepted this month';

  @override
  String get openValueLabel => 'Open value';

  @override
  String get sortOldestFirstLabel => 'Oldest first';

  @override
  String sentAgo(String time) {
    return 'Sent $time';
  }

  @override
  String repliedAgo(String time) {
    return 'Replied $time';
  }

  @override
  String expiresIn(int count) {
    String _temp0 = intl.Intl.pluralLogic(
      count,
      locale: localeName,
      other: 'Expires in $count days',
      one: 'Expires in 1 day',
      zero: 'Expires today',
    );
    return '$_temp0';
  }

  @override
  String expiredLabel(String time) {
    return 'Expired $time';
  }

  @override
  String get quoteBadgeCounterOffer => 'Counter-offer';

  @override
  String get quoteBadgeQuestion => 'Question';

  @override
  String get yourPriceLabel => 'Your price';

  @override
  String get clientAsksLabel => 'Client asks';

  @override
  String get actionSendReminder => 'Send reminder';

  @override
  String get actionRespond => 'Respond';

  @override
  String get actionExtendOrResend => 'Extend or resend';

  @override
  String get actionViewJob => 'View job';

  @override
  String get actionRequote => 'Re-quote';

  @override
  String get reminderSent => 'Reminder logged.';

  @override
  String get boardEmptyOpen => 'Nothing awaiting a client right now.';

  @override
  String get boardEmptyAccepted => 'Nothing accepted yet.';

  @override
  String get boardEmptyRejected => 'Nothing rejected — good sign.';

  @override
  String boardEmptySearch(String query) {
    return 'Nothing matches \"$query\".';
  }

  @override
  String get searchQuotesHint => 'Search client, request ID or quote number';

  @override
  String get quoteDetailTitle => 'Quote history';

  @override
  String timelineSent(int version) {
    return 'Quote v$version sent';
  }

  @override
  String get timelineDraft => 'Draft';

  @override
  String timelineClientCountered(String price) {
    return 'Client countered $price';
  }

  @override
  String get timelineClientAccepted => 'Client accepted';

  @override
  String get timelineClientDeclined => 'Client declined';

  @override
  String get timelineRejectedByYou => 'You rejected the request';

  @override
  String get timelineSupersededLabel => 'Superseded';

  @override
  String get logClientReplyTitle => 'Log the client\'s reply';

  @override
  String get outcomeCounter => 'Countered';

  @override
  String get outcomeCounterSubtitle => 'They proposed a different price';

  @override
  String get outcomeAccepted => 'Accepted';

  @override
  String get outcomeAcceptedSubtitle => 'They agreed to this version';

  @override
  String get outcomeDeclined => 'Declined';

  @override
  String get outcomeDeclinedSubtitle => 'They\'re not moving forward';

  @override
  String get clientPriceLabel => 'Their price per unit';

  @override
  String get noteLabel => 'Note';

  @override
  String get noteOptionalLabel => 'Note (optional)';

  @override
  String get logReply => 'Log reply';

  @override
  String get replyLogged => 'Reply logged.';

  @override
  String get sendNewVersion => 'Send new version';

  @override
  String acceptedVersionLabel(String version) {
    return 'Accepted: $version';
  }

  @override
  String get readOnlyAcceptedNotice =>
      'This quote was accepted. The price is locked.';

  @override
  String get errorClientPriceRequired => 'Enter the client\'s price.';

  @override
  String get negotiationHistoryTitle => 'Negotiation history';

  @override
  String get timelineRequestReceived => 'Request received';

  @override
  String get timelineViaEmail => 'email';

  @override
  String get timelineViaManual => 'manual entry';

  @override
  String timelineReceivedVia(String date, String source) {
    return '$date · by $source';
  }

  @override
  String timelineSentByYouMeta(String date) {
    return '$date · by you';
  }

  @override
  String timelineValidDays(int days) {
    return 'valid $days days';
  }

  @override
  String get timelineYourResponseTitle => 'Your response';

  @override
  String get timelineChooseOption => 'Choose an option below';

  @override
  String get requestLabel => 'Request';

  @override
  String get statusLabel => 'Status';

  @override
  String validUntilLabel(String version) {
    return '$version valid until';
  }

  @override
  String get yourTurnBadge => 'Your turn';

  @override
  String sendNewPriceVersion(String version) {
    return 'Send new price ($version)';
  }

  @override
  String get acceptClientPrice => 'Accept client\'s price';

  @override
  String get logOtherReply => 'Log other reply';

  @override
  String get markDeclined => 'Mark declined';

  @override
  String get acceptDisclaimer =>
      'Accepting sends the job to the coordinator and locks the price.';

  @override
  String get respondToClientTitle => 'Respond to client';

  @override
  String get errorLocationRequired => 'Enter the location.';

  @override
  String get navQueue => 'Queue';

  @override
  String get navMore => 'More';

  @override
  String get statUnassigned => 'Unassigned';

  @override
  String get statScheduled => 'Scheduled';

  @override
  String get statInProgress => 'In progress';

  @override
  String get coordinatorQueueHeading => 'Accepted quotations';

  @override
  String get byDueDate => 'By due date';

  @override
  String get emptyCoordinatorQueue =>
      'Nothing to assign or schedule right now.';

  @override
  String get emptyCoordinatorQueueFiltered =>
      'No jobs in this group right now.';

  @override
  String get assignInspectorAction => 'Assign inspector';

  @override
  String get dueToday => 'Due today';

  @override
  String get dueTomorrow => 'Due tomorrow';

  @override
  String dueInDays(int days) {
    return 'Due in $days days';
  }

  @override
  String overdueByDays(int days) {
    return 'Overdue by $days days';
  }

  @override
  String get assignInspectorTitle => 'Assign inspector';

  @override
  String get pickTimeStepLabel => '1. Pick a time';

  @override
  String get dateLabel => 'Date';

  @override
  String get startTimeLabel => 'Start';

  @override
  String get chooseInspectorStepLabel => '2. Choose inspector';

  @override
  String get assignNotesLabel => '3. Notes (optional)';

  @override
  String get noQualificationsListed => 'No qualifications on file';

  @override
  String get bestMatchTag => 'Best match';

  @override
  String get emptyInspectors => 'No active inspectors yet.';

  @override
  String get errorInspectorRequired => 'Choose an inspector.';

  @override
  String assignAndNotifyAction(String name) {
    return 'Assign & notify $name';
  }

  @override
  String get assignmentSuccessMessage => 'Inspector assigned.';

  @override
  String get filterAll => 'All';

  @override
  String onLeaveUntilLabel(String date) {
    return 'On leave until $date';
  }

  @override
  String get taskAcceptedBadge => 'Accepted';

  @override
  String get navTasks => 'Tasks';

  @override
  String get navCertificates => 'Certificates';

  @override
  String get navTimesheet => 'Timesheet';

  @override
  String get newTaskLabel => 'New task';

  @override
  String get fromYourCoordinatorLabel => 'from your coordinator';

  @override
  String get openAction => 'Open';

  @override
  String get needsResponseBadge => 'Needs response';

  @override
  String get taskInProgressBadge => 'In progress';

  @override
  String get continueCertificateAction => 'Continue certificate';

  @override
  String get acceptAction => 'Accept';

  @override
  String get declineAction => 'Decline';

  @override
  String get declineTaskTitle => 'Decline this task?';

  @override
  String get declineReasonHint => 'Reason (optional)';

  @override
  String get taskAcceptedMessage => 'Task accepted.';

  @override
  String get taskDeclinedMessage => 'Task declined.';

  @override
  String get tasksFilterToday => 'Today';

  @override
  String get tasksFilterUpcoming => 'Upcoming';

  @override
  String get tasksFilterOverdue => 'Overdue';

  @override
  String get tomorrowLabel => 'Tomorrow';

  @override
  String get emptyTasksToday => 'Nothing scheduled for today.';

  @override
  String get emptyTasksUpcoming => 'No upcoming tasks.';

  @override
  String get emptyTasksOverdue => 'No overdue tasks. You are all caught up.';

  @override
  String taskOverdueByDays(int days) {
    String _temp0 = intl.Intl.pluralLogic(
      days,
      locale: localeName,
      other: '$days days overdue',
      one: '1 day overdue',
    );
    return '$_temp0';
  }

  @override
  String get certificateTitle => 'Report of thorough examination';

  @override
  String templateLabel(String id) {
    return 'Template $id';
  }

  @override
  String get savedLabel => 'Saved';

  @override
  String get savingLabel => 'Saving…';

  @override
  String stepOfTotalLabel(int step, int total) {
    return 'Step $step of $total';
  }

  @override
  String get examinationDetailsTitle => 'Examination details';

  @override
  String get itemInformationTitle => 'Item information';

  @override
  String get examinationQuestionsTitle => 'Examination questions';

  @override
  String get defectsTitle => 'Defects & remedial action';

  @override
  String get conclusionTitle => 'Conclusion';

  @override
  String get certificateNumberLabel => 'Certificate number';

  @override
  String get clientAndLocationLabel => 'Client and location';

  @override
  String get clientRepresentativeLabel => 'Client representative';

  @override
  String get examinationDateLabel => 'Examination date';

  @override
  String get nextExaminationDateLabel => 'Next examination date';

  @override
  String get standardOfInspectionLabel => 'Standard of inspection';

  @override
  String get testTypeLabel => 'Test type';

  @override
  String get selectDateHint => 'Select date';

  @override
  String get inspectedItemLabel => 'Inspected item';

  @override
  String get manufacturerLabel => 'Manufacturer';

  @override
  String get modelYearLabel => 'Model / year of manufacture';

  @override
  String get maxWorkingRateLabel => 'Max. working rate';

  @override
  String get serialNumberLabel => 'Serial / chassis number';

  @override
  String get ownerIdLabel => 'Owner ID';

  @override
  String get functionCheckLabel => 'Function check';

  @override
  String get ndtLabel => 'NDT';

  @override
  String get passOption => 'Pass';

  @override
  String get failOption => 'Fail';

  @override
  String get naOption => 'N/A';

  @override
  String get yesOption => 'Yes';

  @override
  String get noOption => 'No';

  @override
  String get questionFirstExamination =>
      'Is this the first examination after installation or assembly at a new site or location?';

  @override
  String get questionInstalledCorrectly =>
      'Has the equipment been installed correctly?';

  @override
  String get examinationCarriedOutLabel => 'Was the examination carried out:';

  @override
  String get questionWithin6Months => 'Within an interval of 6 months?';

  @override
  String get questionWithin12Months => 'Within an interval of 12 months?';

  @override
  String get questionExaminationScheme =>
      'In accordance with an examination scheme?';

  @override
  String get questionExceptionalCircumstances =>
      'After the occurrence of exceptional circumstances?';

  @override
  String get defectDescriptionLabel =>
      'Any part found to have a defect which is or could become a danger to persons, and a description of the defect';

  @override
  String get defectDescriptionHint => 'Leave empty if there is none';

  @override
  String get defectsOptionalSubtitle =>
      'Optional — only if something was found';

  @override
  String get existingDangerLabel =>
      'Is the above an existing or imminent danger to persons? (a reportable defect)';

  @override
  String get futureDangerLabel =>
      'Is the above a defect which is not yet, but could become, a danger to persons?';

  @override
  String get futureDangerByLabel => 'If yes, the date by when';

  @override
  String get repairsRequiredLabel =>
      'Repair, renewal or alteration required to remedy the defect';

  @override
  String get testsCarriedOutLabel =>
      'Tests carried out as part of the examination';

  @override
  String get existingDangerShortLabel => 'Existing danger';

  @override
  String get potentialDangerShortLabel => 'Potential danger';

  @override
  String get safeToOperateOption => 'Safe to operate';

  @override
  String get safeWithConditionsOption =>
      'Safe with conditions (fix in 14 days)';

  @override
  String get notSafeOption => 'Not safe — out of service';

  @override
  String get previewAction => 'Preview';

  @override
  String get submitToTechnicalManagerAction => 'Submit to technical manager';

  @override
  String get confirmSubmitCertificateTitle => 'Submit this certificate?';

  @override
  String get confirmSubmitCertificateMessage =>
      'Once submitted, you won\'t be able to make further changes.';

  @override
  String get submitAction => 'Submit';

  @override
  String get certificateSubmittedMessage => 'Certificate submitted.';

  @override
  String get upcomingBadge => 'Upcoming';

  @override
  String get doneBadge => 'Done';

  @override
  String get returnedBadge => 'Returned';

  @override
  String get myCertificatesTitle => 'My certificates';

  @override
  String get statNeedsAction => 'Needs action';

  @override
  String get statAwaitingReview => 'Awaiting review';

  @override
  String get statSentThisMonth => 'Sent this month';

  @override
  String get searchCertificatesHint => 'Search client, equipment or cert no.';

  @override
  String get filterActionTab => 'Action';

  @override
  String get filterSubmittedTab => 'Submitted';

  @override
  String get filterSentTab => 'Sent';

  @override
  String get notYetSubmittedLabel => 'Not yet submitted';

  @override
  String draftPercentLabel(int percent) {
    return 'Draft · $percent%';
  }

  @override
  String get fixAndResubmitAction => 'Fix & resubmit';

  @override
  String get viewPdfAction => 'View PDF';

  @override
  String submittedRelativeLabel(String relative) {
    return 'Submitted $relative';
  }

  @override
  String get emptyCertificatesList => 'No certificates match.';

  @override
  String get certificateNotFound => 'Certificate details aren\'t available.';

  @override
  String get reviewNavReview => 'Review';

  @override
  String get reviewNavSent => 'Sent';

  @override
  String get reviewQueueTitle => 'Certificates to review';

  @override
  String get statToReview => 'To review';

  @override
  String get statReturned => 'Returned';

  @override
  String get waitingForSignatureTitle => 'Waiting for your signature';

  @override
  String get reviewAndSignAction => 'Review & sign';

  @override
  String byInspectorWhenLabel(String name, String when) {
    return 'By $name · $when';
  }

  @override
  String todayAtTime(String time) {
    return 'today $time';
  }

  @override
  String get yesterdayLabel => 'yesterday';

  @override
  String get returnedToInspector => 'Returned to inspector';

  @override
  String returnedToInspectorNote(String note) {
    return 'Returned to inspector — $note';
  }

  @override
  String get emptyReviewQueue => 'Nothing is waiting for your signature.';

  @override
  String get sentCertificatesTitle => 'Sent certificates';

  @override
  String get emptySentCertificates => 'No certificates have been sent yet.';

  @override
  String sentToLabel(String email) {
    return 'Sent to $email';
  }

  @override
  String get reviewBadgeSafe => 'Safe';

  @override
  String get reviewBadgeConditions => 'With conditions';

  @override
  String get reviewBadgeNotSafe => 'Not safe';

  @override
  String get reviewResultSafe => 'Safe to operate';

  @override
  String get reviewResultConditions => 'Safe with conditions';

  @override
  String get reviewResultNotSafe => 'Not safe — out of service';

  @override
  String get reviewCertificateTitle => 'Review certificate';

  @override
  String get awaitingYouBadge => 'Awaiting you';

  @override
  String get reviewSummaryTitle => 'Summary';

  @override
  String get viewFullPdfAction => 'View full PDF';

  @override
  String get inspectedLabel => 'Inspected';

  @override
  String get nextDueLabel => 'Next due';

  @override
  String get yourCommentsLabel => 'Your comments';

  @override
  String get reviewCommentsHint => 'Add a note for the record (optional)';

  @override
  String get signHereTitle => 'Sign here';

  @override
  String get clearAction => 'Clear';

  @override
  String get savedSignatureTitle => 'Your signature';

  @override
  String get savedSignatureHint =>
      'Used on every certificate you approve. Change it in your Profile.';

  @override
  String get rememberSignatureLabel => 'Remember my signature for next time';

  @override
  String get signatureSheetTitle => 'Draw your signature';

  @override
  String get signatureSheetHint =>
      'Saved to your profile and used every time you approve a certificate.';

  @override
  String get sendSignedPdfToLabel => 'Send signed PDF to';

  @override
  String get returnAction => 'Return';

  @override
  String get approveAndSendAction => 'Approve certificate';

  @override
  String get approveRequirementsHint => 'Add your signature to approve.';

  @override
  String get confirmApproveTitle => 'Approve this certificate?';

  @override
  String confirmApproveMessage(String name) {
    return 'It will be signed as $name and passed to the coordinator to send to the client.';
  }

  @override
  String get approveConfirmAction => 'Approve';

  @override
  String get returnToInspectorTitle => 'Return to inspector';

  @override
  String get returnReasonLabel => 'What needs to change?';

  @override
  String get returnReasonHint =>
      'e.g. Add photos of the landing door interlock';

  @override
  String get returnReasonRequired =>
      'Add a reason so the inspector knows what to fix.';

  @override
  String get returnConfirmAction => 'Return';

  @override
  String get certificateApprovedMessage =>
      'Certificate approved and signed. The coordinator will send it to the client.';

  @override
  String get statToSend => 'To send';

  @override
  String get sendToClientAction => 'Send to client';

  @override
  String get sendCertificateTitle => 'Send certificate';

  @override
  String get sendCertificateHint =>
      'Download the signed PDF, email it to the client, then mark it as sent.';

  @override
  String get confirmSendTitle => 'Mark as sent?';

  @override
  String confirmSendMessage(String email) {
    return 'The certificate will be marked as sent to $email.';
  }

  @override
  String get previewFinalPdfAction => 'Preview final PDF';

  @override
  String get verificationCodeLabel => 'QR verification code';

  @override
  String get noVerificationCodeHint =>
      'Approved before QR codes were introduced, so its QR has no code.';

  @override
  String get certificateSentMessage =>
      'Certificate marked as sent to the client.';

  @override
  String get awaitingCoordinatorLabel => 'With the coordinator to send';

  @override
  String get certificateReturnedMessage =>
      'Certificate returned to the inspector.';

  @override
  String get dateRangeTitle => 'Select dates';

  @override
  String get dateRangeStartLabel => 'Start';

  @override
  String get dateRangeEndLabel => 'End';

  @override
  String get dateRangeSelectHint => 'Tap a start date.';

  @override
  String get dateRangePickEndHint =>
      'Tap an end date, or confirm for a single day.';

  @override
  String get dateRangeReadyHint => 'Ready to confirm.';

  @override
  String get previousMonthTooltip => 'Previous month';

  @override
  String get nextMonthTooltip => 'Next month';

  @override
  String get confirmAction => 'Confirm';

  @override
  String get downloadPdfAction => 'Download PDF';

  @override
  String get preparingPdfMessage => 'Preparing your PDF…';

  @override
  String get pdfPreviewFailedMessage => 'Couldn\'t prepare the PDF.';

  @override
  String get pdfDownloadFailedMessage =>
      'Couldn\'t download the PDF. Try again.';

  @override
  String get timesheetTitle => 'Timesheet';

  @override
  String get timesheetTotalPriceLabel => 'Total';

  @override
  String get timesheetTimeLoggedLabel => 'Time logged';

  @override
  String timesheetLoggedProgress(int logged, int total) {
    return '$logged of $total tasks logged';
  }

  @override
  String get timesheetEmptyMonth => 'No accepted tasks this month.';

  @override
  String get timeSpentLabel => 'Time spent';

  @override
  String get priceLabel => 'Price';

  @override
  String get addTimeAndPriceAction => 'Log time & costs';

  @override
  String get timesheetSheetTitle => 'Time & costs';

  @override
  String get hoursLabel => 'Hours';

  @override
  String get minutesLabel => 'Minutes';

  @override
  String get errorMinutesRange => 'Minutes must be between 0 and 59.';

  @override
  String get errorTimeOrPriceRequired =>
      'Enter the time spent or a transportation cost.';

  @override
  String get timesheetSavedMessage => 'Saved to your timesheet.';

  @override
  String get timesheetPermissionDenied =>
      'You don\'t have access to this timesheet.';

  @override
  String durationHoursMinutes(int hours, int minutes) {
    return '${hours}h ${minutes}m';
  }

  @override
  String durationHours(int hours) {
    return '${hours}h';
  }

  @override
  String durationMinutes(int minutes) {
    return '${minutes}m';
  }

  @override
  String get timesheetStatusPending => 'Awaiting approval';

  @override
  String get timesheetStatusApproved => 'Approved';

  @override
  String get timesheetStatusReturned => 'Returned';

  @override
  String timesheetApprovedBy(String name) {
    return 'Approved by $name';
  }

  @override
  String timesheetReturnedBy(String name) {
    return 'Returned by $name';
  }

  @override
  String get timesheetLockedTooltip => 'Approved — can\'t be changed';

  @override
  String get timesheetActivitiesLabel => 'How the time was spent';

  @override
  String get timesheetActivityInspection => 'Inspection & load testing';

  @override
  String get timesheetExpensesLabel => 'Transportation costs';

  @override
  String get timesheetExpenseInternalTransport => 'Internal transportation';

  @override
  String get timesheetExpenseExternalTransport => 'External transportation';

  @override
  String get timesheetTransportationLabel => 'Transportation';

  @override
  String timesheetIncludingTransportation(String amount) {
    return 'Incl. $amount transportation';
  }

  @override
  String get timesheetActivityReport => 'Report preparation';

  @override
  String get timesheetActivityWaiting => 'Waiting';

  @override
  String get timesheetActivityUnpaidBreak => 'Unpaid break';

  @override
  String get timesheetElapsedLabel => 'Total elapsed';

  @override
  String get timesheetNetTimeLabel => 'Net working time';

  @override
  String get hourlyRateLabel => 'Hourly rate';

  @override
  String hourlyRatePerHour(String rate) {
    return '$rate / hour';
  }

  @override
  String get hourlyRateSetPrompt => 'Set your hourly rate';

  @override
  String get hourlyRateSheetTitle => 'Hourly rate';

  @override
  String get hourlyRateSheetHint =>
      'Your price on each task is worked out from this: the net working time × your hourly rate. It applies to entries you save from now on — entries already logged keep the rate they were saved with.';

  @override
  String get errorHourlyRateRequired => 'Enter an hourly rate.';

  @override
  String get hourlyRateSavedMessage => 'Hourly rate saved.';

  @override
  String timesheetPriceFormula(String time, String rate) {
    return '$time × $rate per hour';
  }

  @override
  String get timesheetRateMissing =>
      'Set your hourly rate on the Timesheet tab to price this entry.';

  @override
  String get timesheetShowDetails => 'Details';

  @override
  String get timesheetHideDetails => 'Hide details';

  @override
  String get timesheetLockedLabel => 'Locked';

  @override
  String timesheetExcludingBreak(String duration) {
    return 'Excl. $duration break';
  }

  @override
  String timesheetPricePlusTransport(String price, String transport) {
    return 'Price $price\n+ $transport transport';
  }

  @override
  String get previewProposal => 'Preview proposal';

  @override
  String get proposalTitle => 'Technical and Commercial Proposal';

  @override
  String get fixedPricePerVisit => 'Fixed price per visit';

  @override
  String get scopeOfWorkLabel => 'Scope of work';

  @override
  String get scopeOfWorkEmpty => 'No points yet — add what the visit covers';

  @override
  String get addScopePoint => 'Add a point';

  @override
  String get proposalNotesLabel => 'Notes on the proposal';

  @override
  String get proposalNotesHelp =>
      'Tap a point to edit it, drag the handle to reorder.';

  @override
  String get addNote => 'Add a note';

  @override
  String get removeNote => 'Remove point';

  @override
  String proposalNotesCount(int count) {
    return '$count points';
  }

  @override
  String proposalNotesStandard(int count) {
    return 'Standard notes · $count points';
  }

  @override
  String get resetNotes => 'Reset to standard';

  @override
  String get newTotalPriceLabel => 'New total price';

  @override
  String get errorTotalPriceRequired => 'Enter the new total price.';

  @override
  String get newPriceSent => 'New price sent.';

  @override
  String get replyToRequestAction => 'Reply to request';
}
