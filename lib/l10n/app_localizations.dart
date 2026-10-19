import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:intl/intl.dart' as intl;

import 'app_localizations_ar.dart';
import 'app_localizations_en.dart';

// ignore_for_file: type=lint

/// Callers can lookup localized strings with an instance of AppLocalizations
/// returned by `AppLocalizations.of(context)`.
///
/// Applications need to include `AppLocalizations.delegate()` in their app's
/// `localizationDelegates` list, and the locales they support in the app's
/// `supportedLocales` list. For example:
///
/// ```dart
/// import 'l10n/app_localizations.dart';
///
/// return MaterialApp(
///   localizationsDelegates: AppLocalizations.localizationsDelegates,
///   supportedLocales: AppLocalizations.supportedLocales,
///   home: MyApplicationHome(),
/// );
/// ```
///
/// ## Update pubspec.yaml
///
/// Please make sure to update your pubspec.yaml to include the following
/// packages:
///
/// ```yaml
/// dependencies:
///   # Internationalization support.
///   flutter_localizations:
///     sdk: flutter
///   intl: any # Use the pinned version from flutter_localizations
///
///   # Rest of dependencies
/// ```
///
/// ## iOS Applications
///
/// iOS applications define key application metadata, including supported
/// locales, in an Info.plist file that is built into the application bundle.
/// To configure the locales supported by your app, you’ll need to edit this
/// file.
///
/// First, open your project’s ios/Runner.xcworkspace Xcode workspace file.
/// Then, in the Project Navigator, open the Info.plist file under the Runner
/// project’s Runner folder.
///
/// Next, select the Information Property List item, select Add Item from the
/// Editor menu, then select Localizations from the pop-up menu.
///
/// Select and expand the newly-created Localizations item then, for each
/// locale your application supports, add a new item and select the locale
/// you wish to add from the pop-up menu in the Value field. This list should
/// be consistent with the languages listed in the AppLocalizations.supportedLocales
/// property.
abstract class AppLocalizations {
  AppLocalizations(String locale)
    : localeName = intl.Intl.canonicalizedLocale(locale.toString());

  final String localeName;

  static AppLocalizations? of(BuildContext context) {
    return Localizations.of<AppLocalizations>(context, AppLocalizations);
  }

  static const LocalizationsDelegate<AppLocalizations> delegate =
      _AppLocalizationsDelegate();

  /// A list of this localizations delegate along with the default localizations
  /// delegates.
  ///
  /// Returns a list of localizations delegates containing this delegate along with
  /// GlobalMaterialLocalizations.delegate, GlobalCupertinoLocalizations.delegate,
  /// and GlobalWidgetsLocalizations.delegate.
  ///
  /// Additional delegates can be added by appending to this list in
  /// MaterialApp. This list does not have to be used at all if a custom list
  /// of delegates is preferred or required.
  static const List<LocalizationsDelegate<dynamic>> localizationsDelegates =
      <LocalizationsDelegate<dynamic>>[
        delegate,
        GlobalMaterialLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
      ];

  /// A list of this localizations delegate's supported locales.
  static const List<Locale> supportedLocales = <Locale>[
    Locale('ar'),
    Locale('en'),
  ];

  /// No description provided for @showPassword.
  ///
  /// In en, this message translates to:
  /// **'Show password'**
  String get showPassword;

  /// No description provided for @hidePassword.
  ///
  /// In en, this message translates to:
  /// **'Hide password'**
  String get hidePassword;

  /// No description provided for @back.
  ///
  /// In en, this message translates to:
  /// **'Back'**
  String get back;

  /// No description provided for @cancel.
  ///
  /// In en, this message translates to:
  /// **'Cancel'**
  String get cancel;

  /// No description provided for @splashStart.
  ///
  /// In en, this message translates to:
  /// **'Get started'**
  String get splashStart;

  /// No description provided for @splashWelcome.
  ///
  /// In en, this message translates to:
  /// **'Welcome to'**
  String get splashWelcome;

  /// No description provided for @splashHint.
  ///
  /// In en, this message translates to:
  /// **'Digital inspections, certificates and requests — all in one place.'**
  String get splashHint;

  /// No description provided for @signInTitle.
  ///
  /// In en, this message translates to:
  /// **'Welcome back'**
  String get signInTitle;

  /// No description provided for @signInSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Sign in to see your requests, tasks and certificates.'**
  String get signInSubtitle;

  /// No description provided for @workEmail.
  ///
  /// In en, this message translates to:
  /// **'Work email'**
  String get workEmail;

  /// No description provided for @emailHint.
  ///
  /// In en, this message translates to:
  /// **'name@company.com'**
  String get emailHint;

  /// No description provided for @password.
  ///
  /// In en, this message translates to:
  /// **'Password'**
  String get password;

  /// No description provided for @keepMeSignedIn.
  ///
  /// In en, this message translates to:
  /// **'Keep me signed in'**
  String get keepMeSignedIn;

  /// No description provided for @forgotPassword.
  ///
  /// In en, this message translates to:
  /// **'Forgot password?'**
  String get forgotPassword;

  /// No description provided for @signIn.
  ///
  /// In en, this message translates to:
  /// **'Sign in'**
  String get signIn;

  /// No description provided for @signInRoleHint.
  ///
  /// In en, this message translates to:
  /// **'You\'ll go straight to your own screens — coordinator, inspector or technical manager.'**
  String get signInRoleHint;

  /// No description provided for @noAccount.
  ///
  /// In en, this message translates to:
  /// **'No account? Ask your admin to add you.'**
  String get noAccount;

  /// No description provided for @resetTitle.
  ///
  /// In en, this message translates to:
  /// **'Reset your password'**
  String get resetTitle;

  /// No description provided for @resetSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Enter your work email and we\'ll send you a link to set a new password.'**
  String get resetSubtitle;

  /// No description provided for @sendResetLink.
  ///
  /// In en, this message translates to:
  /// **'Send reset link'**
  String get sendResetLink;

  /// No description provided for @resendResetLink.
  ///
  /// In en, this message translates to:
  /// **'Send again'**
  String get resendResetLink;

  /// No description provided for @checkInboxTitle.
  ///
  /// In en, this message translates to:
  /// **'Check your inbox'**
  String get checkInboxTitle;

  /// No description provided for @checkInboxMessage.
  ///
  /// In en, this message translates to:
  /// **'If this email has an account, a reset link is on its way. It expires in 1 hour.'**
  String get checkInboxMessage;

  /// No description provided for @rememberedIt.
  ///
  /// In en, this message translates to:
  /// **'Remembered it?'**
  String get rememberedIt;

  /// No description provided for @backToSignIn.
  ///
  /// In en, this message translates to:
  /// **'Back to sign in'**
  String get backToSignIn;

  /// No description provided for @errorEmailRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter your work email.'**
  String get errorEmailRequired;

  /// No description provided for @errorEmailInvalid.
  ///
  /// In en, this message translates to:
  /// **'Enter a valid email address.'**
  String get errorEmailInvalid;

  /// No description provided for @errorPasswordRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter your password.'**
  String get errorPasswordRequired;

  /// No description provided for @errorInvalidCredentials.
  ///
  /// In en, this message translates to:
  /// **'Email or password is incorrect.'**
  String get errorInvalidCredentials;

  /// No description provided for @errorAccountDisabled.
  ///
  /// In en, this message translates to:
  /// **'This account is deactivated. Contact your admin.'**
  String get errorAccountDisabled;

  /// No description provided for @errorNoProfile.
  ///
  /// In en, this message translates to:
  /// **'Your account isn\'t set up yet. Ask your admin to give you a role.'**
  String get errorNoProfile;

  /// No description provided for @errorTooManyAttempts.
  ///
  /// In en, this message translates to:
  /// **'Too many failed attempts. Wait a few minutes and try again.'**
  String get errorTooManyAttempts;

  /// No description provided for @errorNetwork.
  ///
  /// In en, this message translates to:
  /// **'No connection. Check your internet and try again.'**
  String get errorNetwork;

  /// No description provided for @errorUnavailable.
  ///
  /// In en, this message translates to:
  /// **'Sign-in isn\'t available right now. Try again later.'**
  String get errorUnavailable;

  /// No description provided for @errorUnknown.
  ///
  /// In en, this message translates to:
  /// **'Something went wrong. Try again.'**
  String get errorUnknown;

  /// No description provided for @profile.
  ///
  /// In en, this message translates to:
  /// **'Profile'**
  String get profile;

  /// No description provided for @email.
  ///
  /// In en, this message translates to:
  /// **'Email'**
  String get email;

  /// No description provided for @phone.
  ///
  /// In en, this message translates to:
  /// **'Phone'**
  String get phone;

  /// No description provided for @qualifications.
  ///
  /// In en, this message translates to:
  /// **'Qualifications'**
  String get qualifications;

  /// No description provided for @notSet.
  ///
  /// In en, this message translates to:
  /// **'Not set'**
  String get notSet;

  /// No description provided for @signOut.
  ///
  /// In en, this message translates to:
  /// **'Sign out'**
  String get signOut;

  /// No description provided for @signOutConfirmTitle.
  ///
  /// In en, this message translates to:
  /// **'Sign out?'**
  String get signOutConfirmTitle;

  /// No description provided for @signOutConfirmMessage.
  ///
  /// In en, this message translates to:
  /// **'You\'ll need your email and password to sign in again.'**
  String get signOutConfirmMessage;

  /// No description provided for @profileThisMonth.
  ///
  /// In en, this message translates to:
  /// **'This month'**
  String get profileThisMonth;

  /// No description provided for @profileStatQuotesSent.
  ///
  /// In en, this message translates to:
  /// **'Quotes sent'**
  String get profileStatQuotesSent;

  /// No description provided for @profileStatAccepted.
  ///
  /// In en, this message translates to:
  /// **'Accepted'**
  String get profileStatAccepted;

  /// No description provided for @profileStatClients.
  ///
  /// In en, this message translates to:
  /// **'Clients'**
  String get profileStatClients;

  /// No description provided for @profileStatJobsAssigned.
  ///
  /// In en, this message translates to:
  /// **'Jobs assigned'**
  String get profileStatJobsAssigned;

  /// No description provided for @profileStatReassigned.
  ///
  /// In en, this message translates to:
  /// **'Reassigned'**
  String get profileStatReassigned;

  /// No description provided for @profileStatInspectors.
  ///
  /// In en, this message translates to:
  /// **'Inspectors'**
  String get profileStatInspectors;

  /// No description provided for @profileStatInspections.
  ///
  /// In en, this message translates to:
  /// **'Inspections'**
  String get profileStatInspections;

  /// No description provided for @profileStatCertificates.
  ///
  /// In en, this message translates to:
  /// **'Certificates'**
  String get profileStatCertificates;

  /// No description provided for @profileStatReturned.
  ///
  /// In en, this message translates to:
  /// **'Returned'**
  String get profileStatReturned;

  /// No description provided for @profileStatReviewed.
  ///
  /// In en, this message translates to:
  /// **'Reviewed'**
  String get profileStatReviewed;

  /// No description provided for @profileStatSent.
  ///
  /// In en, this message translates to:
  /// **'Sent'**
  String get profileStatSent;

  /// No description provided for @editAction.
  ///
  /// In en, this message translates to:
  /// **'Edit'**
  String get editAction;

  /// No description provided for @changePhotoAction.
  ///
  /// In en, this message translates to:
  /// **'Change photo'**
  String get changePhotoAction;

  /// No description provided for @quotationDefaultsTitle.
  ///
  /// In en, this message translates to:
  /// **'Quotation defaults'**
  String get quotationDefaultsTitle;

  /// No description provided for @quoteValidForLabel.
  ///
  /// In en, this message translates to:
  /// **'Quote valid for'**
  String get quoteValidForLabel;

  /// No description provided for @standardPriceListLabel.
  ///
  /// In en, this message translates to:
  /// **'Standard price list'**
  String get standardPriceListLabel;

  /// No description provided for @viewAction.
  ///
  /// In en, this message translates to:
  /// **'View'**
  String get viewAction;

  /// No description provided for @emailSignatureLabel.
  ///
  /// In en, this message translates to:
  /// **'Email signature'**
  String get emailSignatureLabel;

  /// No description provided for @myWorkTitle.
  ///
  /// In en, this message translates to:
  /// **'My work'**
  String get myWorkTitle;

  /// No description provided for @qualifiedForLabel.
  ///
  /// In en, this message translates to:
  /// **'Qualified for'**
  String get qualifiedForLabel;

  /// No description provided for @availableForTasksLabel.
  ///
  /// In en, this message translates to:
  /// **'Available for new tasks'**
  String get availableForTasksLabel;

  /// No description provided for @requestLeaveAction.
  ///
  /// In en, this message translates to:
  /// **'Request leave'**
  String get requestLeaveAction;

  /// No description provided for @mySignatureTitle.
  ///
  /// In en, this message translates to:
  /// **'My signature'**
  String get mySignatureTitle;

  /// No description provided for @updateAction.
  ///
  /// In en, this message translates to:
  /// **'Update'**
  String get updateAction;

  /// No description provided for @signatureOnFileLabel.
  ///
  /// In en, this message translates to:
  /// **'Signature on file'**
  String get signatureOnFileLabel;

  /// No description provided for @noSignatureLabel.
  ///
  /// In en, this message translates to:
  /// **'No signature added yet'**
  String get noSignatureLabel;

  /// No description provided for @accountSectionTitle.
  ///
  /// In en, this message translates to:
  /// **'Account'**
  String get accountSectionTitle;

  /// No description provided for @personalInfoLabel.
  ///
  /// In en, this message translates to:
  /// **'Personal info'**
  String get personalInfoLabel;

  /// No description provided for @changePasswordLabel.
  ///
  /// In en, this message translates to:
  /// **'Change password'**
  String get changePasswordLabel;

  /// No description provided for @languageLabel.
  ///
  /// In en, this message translates to:
  /// **'Language'**
  String get languageLabel;

  /// No description provided for @helpGuidesLabel.
  ///
  /// In en, this message translates to:
  /// **'Help & guides'**
  String get helpGuidesLabel;

  /// No description provided for @contactAdminLabel.
  ///
  /// In en, this message translates to:
  /// **'Contact admin'**
  String get contactAdminLabel;

  /// No description provided for @versionLabel.
  ///
  /// In en, this message translates to:
  /// **'Version {version}'**
  String versionLabel(String version);

  /// No description provided for @roleCoordinator.
  ///
  /// In en, this message translates to:
  /// **'Coordinator'**
  String get roleCoordinator;

  /// No description provided for @roleInspector.
  ///
  /// In en, this message translates to:
  /// **'Inspector'**
  String get roleInspector;

  /// No description provided for @roleTechnicalManager.
  ///
  /// In en, this message translates to:
  /// **'Technical manager'**
  String get roleTechnicalManager;

  /// No description provided for @roleAdmin.
  ///
  /// In en, this message translates to:
  /// **'Admin'**
  String get roleAdmin;

  /// No description provided for @homeCoordinator.
  ///
  /// In en, this message translates to:
  /// **'Ready to assign'**
  String get homeCoordinator;

  /// No description provided for @homeInspector.
  ///
  /// In en, this message translates to:
  /// **'My tasks'**
  String get homeInspector;

  /// No description provided for @homeTechnicalManager.
  ///
  /// In en, this message translates to:
  /// **'Review queue'**
  String get homeTechnicalManager;

  /// No description provided for @homeAdmin.
  ///
  /// In en, this message translates to:
  /// **'Users'**
  String get homeAdmin;

  /// No description provided for @homeGreeting.
  ///
  /// In en, this message translates to:
  /// **'Hello, {name}'**
  String homeGreeting(String name);

  /// No description provided for @homeComingSoon.
  ///
  /// In en, this message translates to:
  /// **'You\'re signed in and on the right screen. This list is built in a later feature.'**
  String get homeComingSoon;

  /// No description provided for @justNow.
  ///
  /// In en, this message translates to:
  /// **'Just now'**
  String get justNow;

  /// No description provided for @minutesAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, one{1 m ago} other{{count} m ago}}'**
  String minutesAgo(int count);

  /// No description provided for @hoursAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, one{1 h ago} other{{count} h ago}}'**
  String hoursAgo(int count);

  /// No description provided for @daysAgo.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, one{1 d ago} other{{count} d ago}}'**
  String daysAgo(int count);

  /// No description provided for @statusRequestReceived.
  ///
  /// In en, this message translates to:
  /// **'New'**
  String get statusRequestReceived;

  /// No description provided for @statusQuoteDraft.
  ///
  /// In en, this message translates to:
  /// **'Draft'**
  String get statusQuoteDraft;

  /// No description provided for @statusQuoteSent.
  ///
  /// In en, this message translates to:
  /// **'Sent'**
  String get statusQuoteSent;

  /// No description provided for @statusClientCountered.
  ///
  /// In en, this message translates to:
  /// **'Countered'**
  String get statusClientCountered;

  /// No description provided for @statusQuoteRejected.
  ///
  /// In en, this message translates to:
  /// **'Rejected'**
  String get statusQuoteRejected;

  /// No description provided for @statusClientDeclined.
  ///
  /// In en, this message translates to:
  /// **'Declined'**
  String get statusClientDeclined;

  /// No description provided for @statusQuoteAccepted.
  ///
  /// In en, this message translates to:
  /// **'Accepted'**
  String get statusQuoteAccepted;

  /// No description provided for @statusAssigned.
  ///
  /// In en, this message translates to:
  /// **'Assigned'**
  String get statusAssigned;

  /// No description provided for @statusTaskAccepted.
  ///
  /// In en, this message translates to:
  /// **'Task accepted'**
  String get statusTaskAccepted;

  /// No description provided for @statusInProgress.
  ///
  /// In en, this message translates to:
  /// **'In progress'**
  String get statusInProgress;

  /// No description provided for @statusCertificateSubmitted.
  ///
  /// In en, this message translates to:
  /// **'Certificate submitted'**
  String get statusCertificateSubmitted;

  /// No description provided for @statusCertificateReturned.
  ///
  /// In en, this message translates to:
  /// **'Returned'**
  String get statusCertificateReturned;

  /// No description provided for @statusCertificateApproved.
  ///
  /// In en, this message translates to:
  /// **'Approved'**
  String get statusCertificateApproved;

  /// No description provided for @statusSentToClient.
  ///
  /// In en, this message translates to:
  /// **'Sent to client'**
  String get statusSentToClient;

  /// No description provided for @waitingOver24h.
  ///
  /// In en, this message translates to:
  /// **'Waiting 24h+'**
  String get waitingOver24h;

  /// No description provided for @requestsGreetingMorning.
  ///
  /// In en, this message translates to:
  /// **'Good morning'**
  String get requestsGreetingMorning;

  /// No description provided for @requestsGreetingAfternoon.
  ///
  /// In en, this message translates to:
  /// **'Good afternoon'**
  String get requestsGreetingAfternoon;

  /// No description provided for @requestsGreetingEvening.
  ///
  /// In en, this message translates to:
  /// **'Good evening'**
  String get requestsGreetingEvening;

  /// No description provided for @requestsGreetingNameRole.
  ///
  /// In en, this message translates to:
  /// **'{name} · {role}'**
  String requestsGreetingNameRole(String name, String role);

  /// No description provided for @notifications.
  ///
  /// In en, this message translates to:
  /// **'Notifications'**
  String get notifications;

  /// No description provided for @navRequests.
  ///
  /// In en, this message translates to:
  /// **'Requests'**
  String get navRequests;

  /// No description provided for @navQuotations.
  ///
  /// In en, this message translates to:
  /// **'Quotations'**
  String get navQuotations;

  /// No description provided for @navClients.
  ///
  /// In en, this message translates to:
  /// **'Clients'**
  String get navClients;

  /// No description provided for @searchRequestsHint.
  ///
  /// In en, this message translates to:
  /// **'Search client, equipment or ID'**
  String get searchRequestsHint;

  /// No description provided for @tabNew.
  ///
  /// In en, this message translates to:
  /// **'New'**
  String get tabNew;

  /// No description provided for @tabAll.
  ///
  /// In en, this message translates to:
  /// **'All'**
  String get tabAll;

  /// No description provided for @tabCount.
  ///
  /// In en, this message translates to:
  /// **'{label} · {count}'**
  String tabCount(String label, int count);

  /// No description provided for @needsYourReply.
  ///
  /// In en, this message translates to:
  /// **'Needs your reply'**
  String get needsYourReply;

  /// No description provided for @allRequestsHeading.
  ///
  /// In en, this message translates to:
  /// **'All requests'**
  String get allRequestsHeading;

  /// No description provided for @newestFirst.
  ///
  /// In en, this message translates to:
  /// **'Newest first'**
  String get newestFirst;

  /// No description provided for @reviewAndQuote.
  ///
  /// In en, this message translates to:
  /// **'Review & quote'**
  String get reviewAndQuote;

  /// No description provided for @continueQuote.
  ///
  /// In en, this message translates to:
  /// **'Continue quote'**
  String get continueQuote;

  /// No description provided for @clientReplied.
  ///
  /// In en, this message translates to:
  /// **'{name} replied'**
  String clientReplied(String name);

  /// No description provided for @reply.
  ///
  /// In en, this message translates to:
  /// **'Reply'**
  String get reply;

  /// No description provided for @newClientTag.
  ///
  /// In en, this message translates to:
  /// **'New client'**
  String get newClientTag;

  /// No description provided for @unitsCount.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, one{1 unit} other{{count} units}}'**
  String unitsCount(int count);

  /// No description provided for @emptyNewRequests.
  ///
  /// In en, this message translates to:
  /// **'No new requests. Nice and clear.'**
  String get emptyNewRequests;

  /// No description provided for @emptyAllRequests.
  ///
  /// In en, this message translates to:
  /// **'No requests yet.'**
  String get emptyAllRequests;

  /// No description provided for @emptySearch.
  ///
  /// In en, this message translates to:
  /// **'Nothing matches \"{query}\".'**
  String emptySearch(String query);

  /// No description provided for @requestsLoadError.
  ///
  /// In en, this message translates to:
  /// **'Couldn\'t load the requests inbox.'**
  String get requestsLoadError;

  /// No description provided for @requestsPermissionDenied.
  ///
  /// In en, this message translates to:
  /// **'You don\'t have permission to view the requests inbox.'**
  String get requestsPermissionDenied;

  /// No description provided for @retry.
  ///
  /// In en, this message translates to:
  /// **'Retry'**
  String get retry;

  /// No description provided for @requestDetailTitle.
  ///
  /// In en, this message translates to:
  /// **'Request'**
  String get requestDetailTitle;

  /// No description provided for @stepRequest.
  ///
  /// In en, this message translates to:
  /// **'Request'**
  String get stepRequest;

  /// No description provided for @stepQuote.
  ///
  /// In en, this message translates to:
  /// **'Quote'**
  String get stepQuote;

  /// No description provided for @stepAssign.
  ///
  /// In en, this message translates to:
  /// **'Assign'**
  String get stepAssign;

  /// No description provided for @stepInspect.
  ///
  /// In en, this message translates to:
  /// **'Inspect'**
  String get stepInspect;

  /// No description provided for @stepCertify.
  ///
  /// In en, this message translates to:
  /// **'Certify'**
  String get stepCertify;

  /// No description provided for @stepSent.
  ///
  /// In en, this message translates to:
  /// **'Sent'**
  String get stepSent;

  /// No description provided for @clientLabel.
  ///
  /// In en, this message translates to:
  /// **'Client'**
  String get clientLabel;

  /// No description provided for @locationLabel.
  ///
  /// In en, this message translates to:
  /// **'Location'**
  String get locationLabel;

  /// No description provided for @preferredDateLabel.
  ///
  /// In en, this message translates to:
  /// **'Preferred date'**
  String get preferredDateLabel;

  /// No description provided for @accessNotesLabel.
  ///
  /// In en, this message translates to:
  /// **'Access notes'**
  String get accessNotesLabel;

  /// No description provided for @equipmentLabel.
  ///
  /// In en, this message translates to:
  /// **'Equipment'**
  String get equipmentLabel;

  /// No description provided for @capacityLabel.
  ///
  /// In en, this message translates to:
  /// **'Capacity'**
  String get capacityLabel;

  /// No description provided for @quantityLabel.
  ///
  /// In en, this message translates to:
  /// **'Quantity'**
  String get quantityLabel;

  /// No description provided for @readFullEmail.
  ///
  /// In en, this message translates to:
  /// **'Read full email'**
  String get readFullEmail;

  /// No description provided for @notReadyToQuote.
  ///
  /// In en, this message translates to:
  /// **'Add the client, at least one equipment item and the location before quoting.'**
  String get notReadyToQuote;

  /// No description provided for @completeRequestAction.
  ///
  /// In en, this message translates to:
  /// **'Complete request'**
  String get completeRequestAction;

  /// No description provided for @completeRequestSheetTitle.
  ///
  /// In en, this message translates to:
  /// **'Complete this request'**
  String get completeRequestSheetTitle;

  /// No description provided for @completeRequestSheetSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Add the equipment and location this request needs before it can be quoted.'**
  String get completeRequestSheetSubtitle;

  /// No description provided for @addEquipmentItemAction.
  ///
  /// In en, this message translates to:
  /// **'+ Add equipment'**
  String get addEquipmentItemAction;

  /// No description provided for @removeItemAction.
  ///
  /// In en, this message translates to:
  /// **'Remove'**
  String get removeItemAction;

  /// No description provided for @equipmentTypeLabel.
  ///
  /// In en, this message translates to:
  /// **'Equipment type'**
  String get equipmentTypeLabel;

  /// No description provided for @saveAction.
  ///
  /// In en, this message translates to:
  /// **'Save'**
  String get saveAction;

  /// No description provided for @errorEquipmentTypeRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter the equipment type.'**
  String get errorEquipmentTypeRequired;

  /// No description provided for @errorEquipmentRequired.
  ///
  /// In en, this message translates to:
  /// **'Add at least one equipment item.'**
  String get errorEquipmentRequired;

  /// No description provided for @requestCompleted.
  ///
  /// In en, this message translates to:
  /// **'Request completed.'**
  String get requestCompleted;

  /// No description provided for @requestMovedOnMessage.
  ///
  /// In en, this message translates to:
  /// **'A quote already exists for this request — current status: {status}. Manage it from Quotations.'**
  String requestMovedOnMessage(String status);

  /// No description provided for @requestRejectedReasonMessage.
  ///
  /// In en, this message translates to:
  /// **'You rejected this request: {reason}'**
  String requestRejectedReasonMessage(String reason);

  /// No description provided for @viewInQuotationsAction.
  ///
  /// In en, this message translates to:
  /// **'View in Quotations'**
  String get viewInQuotationsAction;

  /// No description provided for @quotationBackendNotBuiltYet.
  ///
  /// In en, this message translates to:
  /// **'Saving and sending aren\'t connected to a server yet.'**
  String get quotationBackendNotBuiltYet;

  /// No description provided for @comingSoon.
  ///
  /// In en, this message translates to:
  /// **'This isn\'t built yet.'**
  String get comingSoon;

  /// No description provided for @howDoYouWantToReply.
  ///
  /// In en, this message translates to:
  /// **'How do you want to reply?'**
  String get howDoYouWantToReply;

  /// No description provided for @replyAccept.
  ///
  /// In en, this message translates to:
  /// **'Accept'**
  String get replyAccept;

  /// No description provided for @replyAcceptSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Use your standard rate'**
  String get replyAcceptSubtitle;

  /// No description provided for @replyOffer.
  ///
  /// In en, this message translates to:
  /// **'Send price offer'**
  String get replyOffer;

  /// No description provided for @replyOfferSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Set a price for this request'**
  String get replyOfferSubtitle;

  /// No description provided for @replyReject.
  ///
  /// In en, this message translates to:
  /// **'Reject'**
  String get replyReject;

  /// No description provided for @replyRejectSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Tell the client why'**
  String get replyRejectSubtitle;

  /// No description provided for @pricePerUnit.
  ///
  /// In en, this message translates to:
  /// **'Price per unit'**
  String get pricePerUnit;

  /// No description provided for @validFor.
  ///
  /// In en, this message translates to:
  /// **'Valid for'**
  String get validFor;

  /// No description provided for @validityDaysOption.
  ///
  /// In en, this message translates to:
  /// **'{days} days'**
  String validityDaysOption(int days);

  /// No description provided for @messageToClient.
  ///
  /// In en, this message translates to:
  /// **'Message to client'**
  String get messageToClient;

  /// No description provided for @optionalLabel.
  ///
  /// In en, this message translates to:
  /// **'optional'**
  String get optionalLabel;

  /// No description provided for @reasonToClient.
  ///
  /// In en, this message translates to:
  /// **'Reason for rejecting'**
  String get reasonToClient;

  /// No description provided for @totalForUnits.
  ///
  /// In en, this message translates to:
  /// **'Total for {units}'**
  String totalForUnits(String units);

  /// No description provided for @standardRateApplies.
  ///
  /// In en, this message translates to:
  /// **'Uses your standard rate'**
  String get standardRateApplies;

  /// No description provided for @saveDraft.
  ///
  /// In en, this message translates to:
  /// **'Save draft'**
  String get saveDraft;

  /// No description provided for @sendQuotation.
  ///
  /// In en, this message translates to:
  /// **'Send quotation'**
  String get sendQuotation;

  /// No description provided for @errorPriceRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter a price per unit.'**
  String get errorPriceRequired;

  /// No description provided for @errorReasonRequired.
  ///
  /// In en, this message translates to:
  /// **'Tell the client why you\'re rejecting.'**
  String get errorReasonRequired;

  /// No description provided for @actionPermissionDenied.
  ///
  /// In en, this message translates to:
  /// **'You don\'t have permission to do that.'**
  String get actionPermissionDenied;

  /// No description provided for @actionFailed.
  ///
  /// In en, this message translates to:
  /// **'Something went wrong. Try again.'**
  String get actionFailed;

  /// No description provided for @draftSaved.
  ///
  /// In en, this message translates to:
  /// **'Draft saved.'**
  String get draftSaved;

  /// No description provided for @quotationSent.
  ///
  /// In en, this message translates to:
  /// **'Quotation sent (v{version}).'**
  String quotationSent(int version);

  /// No description provided for @requestRejected.
  ///
  /// In en, this message translates to:
  /// **'Request rejected.'**
  String get requestRejected;

  /// No description provided for @quotationsTitle.
  ///
  /// In en, this message translates to:
  /// **'Quotations'**
  String get quotationsTitle;

  /// No description provided for @filter.
  ///
  /// In en, this message translates to:
  /// **'Filter'**
  String get filter;

  /// No description provided for @quotationsEmpty.
  ///
  /// In en, this message translates to:
  /// **'No quotations yet.'**
  String get quotationsEmpty;

  /// No description provided for @quoteVersionLabel.
  ///
  /// In en, this message translates to:
  /// **'v{version}'**
  String quoteVersionLabel(int version);

  /// No description provided for @quoteDraftLabel.
  ///
  /// In en, this message translates to:
  /// **'Draft'**
  String get quoteDraftLabel;

  /// No description provided for @quotationExpiredLabel.
  ///
  /// In en, this message translates to:
  /// **'Expired'**
  String get quotationExpiredLabel;

  /// No description provided for @quotationSupersededLabel.
  ///
  /// In en, this message translates to:
  /// **'Superseded'**
  String get quotationSupersededLabel;

  /// No description provided for @requestNoLongerAvailable.
  ///
  /// In en, this message translates to:
  /// **'This request is no longer available.'**
  String get requestNoLongerAvailable;

  /// No description provided for @quoteNumberVersion.
  ///
  /// In en, this message translates to:
  /// **'{quoteNumber} · {version}'**
  String quoteNumberVersion(String quoteNumber, String version);

  /// No description provided for @tabOpen.
  ///
  /// In en, this message translates to:
  /// **'Open'**
  String get tabOpen;

  /// No description provided for @tabReplied.
  ///
  /// In en, this message translates to:
  /// **'Replied'**
  String get tabReplied;

  /// No description provided for @tabAccepted.
  ///
  /// In en, this message translates to:
  /// **'Accepted'**
  String get tabAccepted;

  /// No description provided for @tabLost.
  ///
  /// In en, this message translates to:
  /// **'Lost'**
  String get tabLost;

  /// No description provided for @tileAwaitingClient.
  ///
  /// In en, this message translates to:
  /// **'Awaiting client'**
  String get tileAwaitingClient;

  /// No description provided for @tileClientReplied.
  ///
  /// In en, this message translates to:
  /// **'Client replied'**
  String get tileClientReplied;

  /// No description provided for @tileExpiringSoon.
  ///
  /// In en, this message translates to:
  /// **'Expiring soon'**
  String get tileExpiringSoon;

  /// No description provided for @tileAcceptedThisMonth.
  ///
  /// In en, this message translates to:
  /// **'Accepted this month'**
  String get tileAcceptedThisMonth;

  /// No description provided for @openValueLabel.
  ///
  /// In en, this message translates to:
  /// **'Open value'**
  String get openValueLabel;

  /// No description provided for @sortOldestFirstLabel.
  ///
  /// In en, this message translates to:
  /// **'Oldest first'**
  String get sortOldestFirstLabel;

  /// No description provided for @sentAgo.
  ///
  /// In en, this message translates to:
  /// **'Sent {time}'**
  String sentAgo(String time);

  /// No description provided for @repliedAgo.
  ///
  /// In en, this message translates to:
  /// **'Replied {time}'**
  String repliedAgo(String time);

  /// No description provided for @expiresIn.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, =0{Expires today} one{Expires in 1 day} other{Expires in {count} days}}'**
  String expiresIn(int count);

  /// No description provided for @expiredLabel.
  ///
  /// In en, this message translates to:
  /// **'Expired {time}'**
  String expiredLabel(String time);

  /// No description provided for @quoteBadgeCounterOffer.
  ///
  /// In en, this message translates to:
  /// **'Counter-offer'**
  String get quoteBadgeCounterOffer;

  /// No description provided for @quoteBadgeQuestion.
  ///
  /// In en, this message translates to:
  /// **'Question'**
  String get quoteBadgeQuestion;

  /// No description provided for @yourPriceLabel.
  ///
  /// In en, this message translates to:
  /// **'Your price'**
  String get yourPriceLabel;

  /// No description provided for @clientAsksLabel.
  ///
  /// In en, this message translates to:
  /// **'Client asks'**
  String get clientAsksLabel;

  /// No description provided for @actionSendReminder.
  ///
  /// In en, this message translates to:
  /// **'Send reminder'**
  String get actionSendReminder;

  /// No description provided for @actionRespond.
  ///
  /// In en, this message translates to:
  /// **'Respond'**
  String get actionRespond;

  /// No description provided for @actionExtendOrResend.
  ///
  /// In en, this message translates to:
  /// **'Extend or resend'**
  String get actionExtendOrResend;

  /// No description provided for @actionViewJob.
  ///
  /// In en, this message translates to:
  /// **'View job'**
  String get actionViewJob;

  /// No description provided for @actionRequote.
  ///
  /// In en, this message translates to:
  /// **'Re-quote'**
  String get actionRequote;

  /// No description provided for @reminderSent.
  ///
  /// In en, this message translates to:
  /// **'Reminder logged.'**
  String get reminderSent;

  /// No description provided for @boardEmptyOpen.
  ///
  /// In en, this message translates to:
  /// **'Nothing awaiting a client right now.'**
  String get boardEmptyOpen;

  /// No description provided for @boardEmptyReplied.
  ///
  /// In en, this message translates to:
  /// **'No replies waiting on you.'**
  String get boardEmptyReplied;

  /// No description provided for @boardEmptyAccepted.
  ///
  /// In en, this message translates to:
  /// **'Nothing accepted yet.'**
  String get boardEmptyAccepted;

  /// No description provided for @boardEmptyLost.
  ///
  /// In en, this message translates to:
  /// **'Nothing lost — good sign.'**
  String get boardEmptyLost;

  /// No description provided for @boardEmptySearch.
  ///
  /// In en, this message translates to:
  /// **'Nothing matches \"{query}\".'**
  String boardEmptySearch(String query);

  /// No description provided for @searchQuotesHint.
  ///
  /// In en, this message translates to:
  /// **'Search client, request ID or quote number'**
  String get searchQuotesHint;

  /// No description provided for @quoteDetailTitle.
  ///
  /// In en, this message translates to:
  /// **'Quote history'**
  String get quoteDetailTitle;

  /// No description provided for @timelineSent.
  ///
  /// In en, this message translates to:
  /// **'Quote v{version} sent'**
  String timelineSent(int version);

  /// No description provided for @timelineDraft.
  ///
  /// In en, this message translates to:
  /// **'Draft'**
  String get timelineDraft;

  /// No description provided for @timelineClientCountered.
  ///
  /// In en, this message translates to:
  /// **'Client countered {price}'**
  String timelineClientCountered(String price);

  /// No description provided for @timelineClientAccepted.
  ///
  /// In en, this message translates to:
  /// **'Client accepted'**
  String get timelineClientAccepted;

  /// No description provided for @timelineClientDeclined.
  ///
  /// In en, this message translates to:
  /// **'Client declined'**
  String get timelineClientDeclined;

  /// No description provided for @timelineRejectedByYou.
  ///
  /// In en, this message translates to:
  /// **'You rejected the request'**
  String get timelineRejectedByYou;

  /// No description provided for @timelineSupersededLabel.
  ///
  /// In en, this message translates to:
  /// **'Superseded'**
  String get timelineSupersededLabel;

  /// No description provided for @logClientReplyTitle.
  ///
  /// In en, this message translates to:
  /// **'Log the client\'s reply'**
  String get logClientReplyTitle;

  /// No description provided for @outcomeCounter.
  ///
  /// In en, this message translates to:
  /// **'Countered'**
  String get outcomeCounter;

  /// No description provided for @outcomeCounterSubtitle.
  ///
  /// In en, this message translates to:
  /// **'They proposed a different price'**
  String get outcomeCounterSubtitle;

  /// No description provided for @outcomeAccepted.
  ///
  /// In en, this message translates to:
  /// **'Accepted'**
  String get outcomeAccepted;

  /// No description provided for @outcomeAcceptedSubtitle.
  ///
  /// In en, this message translates to:
  /// **'They agreed to this version'**
  String get outcomeAcceptedSubtitle;

  /// No description provided for @outcomeDeclined.
  ///
  /// In en, this message translates to:
  /// **'Declined'**
  String get outcomeDeclined;

  /// No description provided for @outcomeDeclinedSubtitle.
  ///
  /// In en, this message translates to:
  /// **'They\'re not moving forward'**
  String get outcomeDeclinedSubtitle;

  /// No description provided for @clientPriceLabel.
  ///
  /// In en, this message translates to:
  /// **'Their price per unit'**
  String get clientPriceLabel;

  /// No description provided for @noteLabel.
  ///
  /// In en, this message translates to:
  /// **'Note'**
  String get noteLabel;

  /// No description provided for @noteOptionalLabel.
  ///
  /// In en, this message translates to:
  /// **'Note (optional)'**
  String get noteOptionalLabel;

  /// No description provided for @logReply.
  ///
  /// In en, this message translates to:
  /// **'Log reply'**
  String get logReply;

  /// No description provided for @replyLogged.
  ///
  /// In en, this message translates to:
  /// **'Reply logged.'**
  String get replyLogged;

  /// No description provided for @sendNewVersion.
  ///
  /// In en, this message translates to:
  /// **'Send new version'**
  String get sendNewVersion;

  /// No description provided for @acceptedVersionLabel.
  ///
  /// In en, this message translates to:
  /// **'Accepted: {version}'**
  String acceptedVersionLabel(String version);

  /// No description provided for @readOnlyAcceptedNotice.
  ///
  /// In en, this message translates to:
  /// **'This quote was accepted. The price is locked.'**
  String get readOnlyAcceptedNotice;

  /// No description provided for @errorClientPriceRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter the client\'s price.'**
  String get errorClientPriceRequired;

  /// No description provided for @negotiationHistoryTitle.
  ///
  /// In en, this message translates to:
  /// **'Negotiation history'**
  String get negotiationHistoryTitle;

  /// No description provided for @timelineRequestReceived.
  ///
  /// In en, this message translates to:
  /// **'Request received'**
  String get timelineRequestReceived;

  /// No description provided for @timelineViaEmail.
  ///
  /// In en, this message translates to:
  /// **'email'**
  String get timelineViaEmail;

  /// No description provided for @timelineViaManual.
  ///
  /// In en, this message translates to:
  /// **'manual entry'**
  String get timelineViaManual;

  /// No description provided for @timelineReceivedVia.
  ///
  /// In en, this message translates to:
  /// **'{date} · by {source}'**
  String timelineReceivedVia(String date, String source);

  /// No description provided for @timelineSentByYouMeta.
  ///
  /// In en, this message translates to:
  /// **'{date} · by you'**
  String timelineSentByYouMeta(String date);

  /// No description provided for @timelineValidDays.
  ///
  /// In en, this message translates to:
  /// **'valid {days} days'**
  String timelineValidDays(int days);

  /// No description provided for @timelineYourResponseTitle.
  ///
  /// In en, this message translates to:
  /// **'Your response'**
  String get timelineYourResponseTitle;

  /// No description provided for @timelineChooseOption.
  ///
  /// In en, this message translates to:
  /// **'Choose an option below'**
  String get timelineChooseOption;

  /// No description provided for @requestLabel.
  ///
  /// In en, this message translates to:
  /// **'Request'**
  String get requestLabel;

  /// No description provided for @statusLabel.
  ///
  /// In en, this message translates to:
  /// **'Status'**
  String get statusLabel;

  /// No description provided for @validUntilLabel.
  ///
  /// In en, this message translates to:
  /// **'{version} valid until'**
  String validUntilLabel(String version);

  /// No description provided for @yourTurnBadge.
  ///
  /// In en, this message translates to:
  /// **'Your turn'**
  String get yourTurnBadge;

  /// No description provided for @sendNewPriceVersion.
  ///
  /// In en, this message translates to:
  /// **'Send new price ({version})'**
  String sendNewPriceVersion(String version);

  /// No description provided for @acceptClientPrice.
  ///
  /// In en, this message translates to:
  /// **'Accept client\'s price'**
  String get acceptClientPrice;

  /// No description provided for @logOtherReply.
  ///
  /// In en, this message translates to:
  /// **'Log other reply'**
  String get logOtherReply;

  /// No description provided for @markDeclined.
  ///
  /// In en, this message translates to:
  /// **'Mark declined'**
  String get markDeclined;

  /// No description provided for @acceptDisclaimer.
  ///
  /// In en, this message translates to:
  /// **'Accepting sends the job to the coordinator and locks the price.'**
  String get acceptDisclaimer;

  /// No description provided for @respondToClientTitle.
  ///
  /// In en, this message translates to:
  /// **'Respond to client'**
  String get respondToClientTitle;

  /// No description provided for @clientsTitle.
  ///
  /// In en, this message translates to:
  /// **'Clients'**
  String get clientsTitle;

  /// No description provided for @addClient.
  ///
  /// In en, this message translates to:
  /// **'Add client'**
  String get addClient;

  /// No description provided for @searchClientsHint.
  ///
  /// In en, this message translates to:
  /// **'Search company, contact or email'**
  String get searchClientsHint;

  /// No description provided for @clientsFilterAll.
  ///
  /// In en, this message translates to:
  /// **'All'**
  String get clientsFilterAll;

  /// No description provided for @clientsFilterOpenJobs.
  ///
  /// In en, this message translates to:
  /// **'Open jobs'**
  String get clientsFilterOpenJobs;

  /// No description provided for @clientsFilterDueSoon.
  ///
  /// In en, this message translates to:
  /// **'Due soon'**
  String get clientsFilterDueSoon;

  /// No description provided for @clientsSortedByActivity.
  ///
  /// In en, this message translates to:
  /// **'Sorted by latest activity'**
  String get clientsSortedByActivity;

  /// No description provided for @equipmentCountChip.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, one{1 equipment} other{{count} equipment}}'**
  String equipmentCountChip(int count);

  /// No description provided for @openJobsCountChip.
  ///
  /// In en, this message translates to:
  /// **'{count, plural, one{1 open job} other{{count} open jobs}}'**
  String openJobsCountChip(int count);

  /// No description provided for @dueSoonCountChip.
  ///
  /// In en, this message translates to:
  /// **'{count} due in 30 days'**
  String dueSoonCountChip(int count);

  /// No description provided for @newSenderNeedsClient.
  ///
  /// In en, this message translates to:
  /// **'New sender needs a client'**
  String get newSenderNeedsClient;

  /// No description provided for @matchAction.
  ///
  /// In en, this message translates to:
  /// **'Match'**
  String get matchAction;

  /// No description provided for @clientsEmpty.
  ///
  /// In en, this message translates to:
  /// **'No clients yet.'**
  String get clientsEmpty;

  /// No description provided for @clientDetailEdit.
  ///
  /// In en, this message translates to:
  /// **'Edit'**
  String get clientDetailEdit;

  /// No description provided for @newRequestButton.
  ///
  /// In en, this message translates to:
  /// **'New request'**
  String get newRequestButton;

  /// No description provided for @callAction.
  ///
  /// In en, this message translates to:
  /// **'Call'**
  String get callAction;

  /// No description provided for @emailAction.
  ///
  /// In en, this message translates to:
  /// **'Email'**
  String get emailAction;

  /// No description provided for @phoneCopied.
  ///
  /// In en, this message translates to:
  /// **'Phone number copied.'**
  String get phoneCopied;

  /// No description provided for @emailCopied.
  ///
  /// In en, this message translates to:
  /// **'Email copied.'**
  String get emailCopied;

  /// No description provided for @noPhoneNumber.
  ///
  /// In en, this message translates to:
  /// **'No phone number on file.'**
  String get noPhoneNumber;

  /// No description provided for @noEmailOnFile.
  ///
  /// In en, this message translates to:
  /// **'No email on file.'**
  String get noEmailOnFile;

  /// No description provided for @equipmentStatLabel.
  ///
  /// In en, this message translates to:
  /// **'Equipment'**
  String get equipmentStatLabel;

  /// No description provided for @openJobsStatLabel.
  ///
  /// In en, this message translates to:
  /// **'Open jobs'**
  String get openJobsStatLabel;

  /// No description provided for @dueSoonStatLabel.
  ///
  /// In en, this message translates to:
  /// **'Due in 30 days'**
  String get dueSoonStatLabel;

  /// No description provided for @contactsTitle.
  ///
  /// In en, this message translates to:
  /// **'Contacts'**
  String get contactsTitle;

  /// No description provided for @addContactAction.
  ///
  /// In en, this message translates to:
  /// **'+ Add'**
  String get addContactAction;

  /// No description provided for @mainContactBadge.
  ///
  /// In en, this message translates to:
  /// **'Main'**
  String get mainContactBadge;

  /// No description provided for @certificatesSentToLabel.
  ///
  /// In en, this message translates to:
  /// **'Certificates are sent to'**
  String get certificatesSentToLabel;

  /// No description provided for @changeAction.
  ///
  /// In en, this message translates to:
  /// **'Change'**
  String get changeAction;

  /// No description provided for @noCertificatesEmail.
  ///
  /// In en, this message translates to:
  /// **'Not set'**
  String get noCertificatesEmail;

  /// No description provided for @historyTabRequests.
  ///
  /// In en, this message translates to:
  /// **'Requests'**
  String get historyTabRequests;

  /// No description provided for @historyTabEquipment.
  ///
  /// In en, this message translates to:
  /// **'Equipment'**
  String get historyTabEquipment;

  /// No description provided for @historyTabCertificates.
  ///
  /// In en, this message translates to:
  /// **'Certificates'**
  String get historyTabCertificates;

  /// No description provided for @clientHistoryEmptyRequests.
  ///
  /// In en, this message translates to:
  /// **'No requests from this client yet.'**
  String get clientHistoryEmptyRequests;

  /// No description provided for @clientHistoryEmptyEquipment.
  ///
  /// In en, this message translates to:
  /// **'No equipment on record yet.'**
  String get clientHistoryEmptyEquipment;

  /// No description provided for @clientHistoryEmptyCertificates.
  ///
  /// In en, this message translates to:
  /// **'Certificates aren\'t tracked yet.'**
  String get clientHistoryEmptyCertificates;

  /// No description provided for @addClientSheetTitle.
  ///
  /// In en, this message translates to:
  /// **'Add client'**
  String get addClientSheetTitle;

  /// No description provided for @companyNameLabel.
  ///
  /// In en, this message translates to:
  /// **'Company name'**
  String get companyNameLabel;

  /// No description provided for @contactNameLabel.
  ///
  /// In en, this message translates to:
  /// **'Contact name'**
  String get contactNameLabel;

  /// No description provided for @roleLabel.
  ///
  /// In en, this message translates to:
  /// **'Role'**
  String get roleLabel;

  /// No description provided for @emailLabel.
  ///
  /// In en, this message translates to:
  /// **'Email'**
  String get emailLabel;

  /// No description provided for @phoneLabel.
  ///
  /// In en, this message translates to:
  /// **'Phone'**
  String get phoneLabel;

  /// No description provided for @createAction.
  ///
  /// In en, this message translates to:
  /// **'Create'**
  String get createAction;

  /// No description provided for @errorCompanyNameRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter the company name.'**
  String get errorCompanyNameRequired;

  /// No description provided for @errorContactNameRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter the contact\'s name.'**
  String get errorContactNameRequired;

  /// No description provided for @errorLocationRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter the location.'**
  String get errorLocationRequired;

  /// No description provided for @clientCreated.
  ///
  /// In en, this message translates to:
  /// **'Client added.'**
  String get clientCreated;

  /// No description provided for @addContactSheetTitle.
  ///
  /// In en, this message translates to:
  /// **'Add contact'**
  String get addContactSheetTitle;

  /// No description provided for @contactAdded.
  ///
  /// In en, this message translates to:
  /// **'Contact added.'**
  String get contactAdded;

  /// No description provided for @matchSheetTitle.
  ///
  /// In en, this message translates to:
  /// **'Match to a client'**
  String get matchSheetTitle;

  /// No description provided for @matchNewClientOption.
  ///
  /// In en, this message translates to:
  /// **'New client'**
  String get matchNewClientOption;

  /// No description provided for @clientMatched.
  ///
  /// In en, this message translates to:
  /// **'Matched to client.'**
  String get clientMatched;

  /// No description provided for @certificatesEmailUpdated.
  ///
  /// In en, this message translates to:
  /// **'Certificates email updated.'**
  String get certificatesEmailUpdated;

  /// No description provided for @navQueue.
  ///
  /// In en, this message translates to:
  /// **'Queue'**
  String get navQueue;

  /// No description provided for @navInspectors.
  ///
  /// In en, this message translates to:
  /// **'Inspectors'**
  String get navInspectors;

  /// No description provided for @navMore.
  ///
  /// In en, this message translates to:
  /// **'More'**
  String get navMore;

  /// No description provided for @statUnassigned.
  ///
  /// In en, this message translates to:
  /// **'Unassigned'**
  String get statUnassigned;

  /// No description provided for @statScheduled.
  ///
  /// In en, this message translates to:
  /// **'Scheduled'**
  String get statScheduled;

  /// No description provided for @statInProgress.
  ///
  /// In en, this message translates to:
  /// **'In progress'**
  String get statInProgress;

  /// No description provided for @coordinatorQueueHeading.
  ///
  /// In en, this message translates to:
  /// **'Accepted quotations'**
  String get coordinatorQueueHeading;

  /// No description provided for @byDueDate.
  ///
  /// In en, this message translates to:
  /// **'By due date'**
  String get byDueDate;

  /// No description provided for @emptyCoordinatorQueue.
  ///
  /// In en, this message translates to:
  /// **'Nothing to assign or schedule right now.'**
  String get emptyCoordinatorQueue;

  /// No description provided for @emptyCoordinatorQueueFiltered.
  ///
  /// In en, this message translates to:
  /// **'No jobs in this group right now.'**
  String get emptyCoordinatorQueueFiltered;

  /// No description provided for @assignInspectorAction.
  ///
  /// In en, this message translates to:
  /// **'Assign inspector'**
  String get assignInspectorAction;

  /// No description provided for @dueToday.
  ///
  /// In en, this message translates to:
  /// **'Due today'**
  String get dueToday;

  /// No description provided for @dueTomorrow.
  ///
  /// In en, this message translates to:
  /// **'Due tomorrow'**
  String get dueTomorrow;

  /// No description provided for @dueInDays.
  ///
  /// In en, this message translates to:
  /// **'Due in {days} days'**
  String dueInDays(int days);

  /// No description provided for @overdueByDays.
  ///
  /// In en, this message translates to:
  /// **'Overdue by {days} days'**
  String overdueByDays(int days);

  /// No description provided for @assignInspectorTitle.
  ///
  /// In en, this message translates to:
  /// **'Assign inspector'**
  String get assignInspectorTitle;

  /// No description provided for @pickTimeStepLabel.
  ///
  /// In en, this message translates to:
  /// **'1. Pick a time'**
  String get pickTimeStepLabel;

  /// No description provided for @dateLabel.
  ///
  /// In en, this message translates to:
  /// **'Date'**
  String get dateLabel;

  /// No description provided for @startTimeLabel.
  ///
  /// In en, this message translates to:
  /// **'Start'**
  String get startTimeLabel;

  /// No description provided for @chooseInspectorStepLabel.
  ///
  /// In en, this message translates to:
  /// **'2. Choose inspector'**
  String get chooseInspectorStepLabel;

  /// No description provided for @assignNotesLabel.
  ///
  /// In en, this message translates to:
  /// **'3. Notes (optional)'**
  String get assignNotesLabel;

  /// No description provided for @noQualificationsListed.
  ///
  /// In en, this message translates to:
  /// **'No qualifications on file'**
  String get noQualificationsListed;

  /// No description provided for @bestMatchTag.
  ///
  /// In en, this message translates to:
  /// **'Best match'**
  String get bestMatchTag;

  /// No description provided for @emptyInspectors.
  ///
  /// In en, this message translates to:
  /// **'No active inspectors yet.'**
  String get emptyInspectors;

  /// No description provided for @errorInspectorRequired.
  ///
  /// In en, this message translates to:
  /// **'Choose an inspector.'**
  String get errorInspectorRequired;

  /// No description provided for @assignAndNotifyAction.
  ///
  /// In en, this message translates to:
  /// **'Assign & notify {name}'**
  String assignAndNotifyAction(String name);

  /// No description provided for @assignmentSuccessMessage.
  ///
  /// In en, this message translates to:
  /// **'Inspector assigned.'**
  String get assignmentSuccessMessage;

  /// No description provided for @inspectorsTitle.
  ///
  /// In en, this message translates to:
  /// **'Inspectors'**
  String get inspectorsTitle;

  /// No description provided for @searchInspectorsHint.
  ///
  /// In en, this message translates to:
  /// **'Search by name'**
  String get searchInspectorsHint;

  /// No description provided for @filterAll.
  ///
  /// In en, this message translates to:
  /// **'All'**
  String get filterAll;

  /// No description provided for @statFreeToday.
  ///
  /// In en, this message translates to:
  /// **'Free today'**
  String get statFreeToday;

  /// No description provided for @statBusy.
  ///
  /// In en, this message translates to:
  /// **'Busy'**
  String get statBusy;

  /// No description provided for @statOnLeave.
  ///
  /// In en, this message translates to:
  /// **'On leave'**
  String get statOnLeave;

  /// No description provided for @freeBadge.
  ///
  /// In en, this message translates to:
  /// **'Free'**
  String get freeBadge;

  /// No description provided for @busyBadge.
  ///
  /// In en, this message translates to:
  /// **'Busy'**
  String get busyBadge;

  /// No description provided for @onLeaveBadge.
  ///
  /// In en, this message translates to:
  /// **'Leave'**
  String get onLeaveBadge;

  /// No description provided for @onLeaveUntilLabel.
  ///
  /// In en, this message translates to:
  /// **'On leave until {date}'**
  String onLeaveUntilLabel(String date);

  /// No description provided for @todayLabel.
  ///
  /// In en, this message translates to:
  /// **'Today'**
  String get todayLabel;

  /// No description provided for @slotsOfCapacity.
  ///
  /// In en, this message translates to:
  /// **'{used} of {capacity} slots'**
  String slotsOfCapacity(int used, int capacity);

  /// No description provided for @nowLabel.
  ///
  /// In en, this message translates to:
  /// **'Now:'**
  String get nowLabel;

  /// No description provided for @nextLabel.
  ///
  /// In en, this message translates to:
  /// **'Next:'**
  String get nextLabel;

  /// No description provided for @licenseExpiresInDays.
  ///
  /// In en, this message translates to:
  /// **'{name} license expires in {days} days'**
  String licenseExpiresInDays(String name, int days);

  /// No description provided for @emptyInspectorsRoster.
  ///
  /// In en, this message translates to:
  /// **'No inspectors match.'**
  String get emptyInspectorsRoster;

  /// No description provided for @assignAJobAction.
  ///
  /// In en, this message translates to:
  /// **'Assign a job'**
  String get assignAJobAction;

  /// No description provided for @pickJobToAssignTitle.
  ///
  /// In en, this message translates to:
  /// **'Assign a job'**
  String get pickJobToAssignTitle;

  /// No description provided for @chatAction.
  ///
  /// In en, this message translates to:
  /// **'Chat'**
  String get chatAction;

  /// No description provided for @doneThisMonthLabel.
  ///
  /// In en, this message translates to:
  /// **'Done this month'**
  String get doneThisMonthLabel;

  /// No description provided for @onTimeLabel.
  ///
  /// In en, this message translates to:
  /// **'On time'**
  String get onTimeLabel;

  /// No description provided for @returnedLabel.
  ///
  /// In en, this message translates to:
  /// **'Returned'**
  String get returnedLabel;

  /// No description provided for @thisWeekTitle.
  ///
  /// In en, this message translates to:
  /// **'This week'**
  String get thisWeekTitle;

  /// No description provided for @tasksPerDayCaption.
  ///
  /// In en, this message translates to:
  /// **'Tasks per day · red = fully booked'**
  String get tasksPerDayCaption;

  /// No description provided for @offLabel.
  ///
  /// In en, this message translates to:
  /// **'off'**
  String get offLabel;

  /// No description provided for @qualificationsTitle.
  ///
  /// In en, this message translates to:
  /// **'Qualifications'**
  String get qualificationsTitle;

  /// No description provided for @validToLabel.
  ///
  /// In en, this message translates to:
  /// **'Valid to {date}'**
  String validToLabel(String date);

  /// No description provided for @expiresOnLabel.
  ///
  /// In en, this message translates to:
  /// **'Expires {date}'**
  String expiresOnLabel(String date);

  /// No description provided for @upcomingTasksTitle.
  ///
  /// In en, this message translates to:
  /// **'Upcoming tasks'**
  String get upcomingTasksTitle;

  /// No description provided for @seeAllAction.
  ///
  /// In en, this message translates to:
  /// **'See all'**
  String get seeAllAction;

  /// No description provided for @emptyUpcomingTasks.
  ///
  /// In en, this message translates to:
  /// **'No upcoming tasks.'**
  String get emptyUpcomingTasks;

  /// No description provided for @taskAcceptedBadge.
  ///
  /// In en, this message translates to:
  /// **'Accepted'**
  String get taskAcceptedBadge;

  /// No description provided for @taskNotAcceptedBadge.
  ///
  /// In en, this message translates to:
  /// **'Not accepted'**
  String get taskNotAcceptedBadge;

  /// No description provided for @allTasksTitle.
  ///
  /// In en, this message translates to:
  /// **'All tasks'**
  String get allTasksTitle;

  /// No description provided for @markLeaveAction.
  ///
  /// In en, this message translates to:
  /// **'Mark leave'**
  String get markLeaveAction;

  /// No description provided for @leaveMarkedMessage.
  ///
  /// In en, this message translates to:
  /// **'Leave marked.'**
  String get leaveMarkedMessage;

  /// No description provided for @onSiteNowBadge.
  ///
  /// In en, this message translates to:
  /// **'On site now'**
  String get onSiteNowBadge;

  /// No description provided for @scheduleFreeAllDayLabel.
  ///
  /// In en, this message translates to:
  /// **'Free all day'**
  String get scheduleFreeAllDayLabel;

  /// No description provided for @navTasks.
  ///
  /// In en, this message translates to:
  /// **'Tasks'**
  String get navTasks;

  /// No description provided for @navCertificates.
  ///
  /// In en, this message translates to:
  /// **'Certificates'**
  String get navCertificates;

  /// No description provided for @navTimesheet.
  ///
  /// In en, this message translates to:
  /// **'Timesheet'**
  String get navTimesheet;

  /// No description provided for @newTaskLabel.
  ///
  /// In en, this message translates to:
  /// **'New task'**
  String get newTaskLabel;

  /// No description provided for @fromYourCoordinatorLabel.
  ///
  /// In en, this message translates to:
  /// **'from your coordinator'**
  String get fromYourCoordinatorLabel;

  /// No description provided for @openAction.
  ///
  /// In en, this message translates to:
  /// **'Open'**
  String get openAction;

  /// No description provided for @needsResponseBadge.
  ///
  /// In en, this message translates to:
  /// **'Needs response'**
  String get needsResponseBadge;

  /// No description provided for @taskInProgressBadge.
  ///
  /// In en, this message translates to:
  /// **'In progress'**
  String get taskInProgressBadge;

  /// No description provided for @continueCertificateAction.
  ///
  /// In en, this message translates to:
  /// **'Continue certificate'**
  String get continueCertificateAction;

  /// No description provided for @acceptAction.
  ///
  /// In en, this message translates to:
  /// **'Accept'**
  String get acceptAction;

  /// No description provided for @declineAction.
  ///
  /// In en, this message translates to:
  /// **'Decline'**
  String get declineAction;

  /// No description provided for @declineTaskTitle.
  ///
  /// In en, this message translates to:
  /// **'Decline this task?'**
  String get declineTaskTitle;

  /// No description provided for @declineReasonHint.
  ///
  /// In en, this message translates to:
  /// **'Reason (optional)'**
  String get declineReasonHint;

  /// No description provided for @taskAcceptedMessage.
  ///
  /// In en, this message translates to:
  /// **'Task accepted.'**
  String get taskAcceptedMessage;

  /// No description provided for @taskDeclinedMessage.
  ///
  /// In en, this message translates to:
  /// **'Task declined.'**
  String get taskDeclinedMessage;

  /// No description provided for @tasksFilterToday.
  ///
  /// In en, this message translates to:
  /// **'Today'**
  String get tasksFilterToday;

  /// No description provided for @tasksFilterUpcoming.
  ///
  /// In en, this message translates to:
  /// **'Upcoming'**
  String get tasksFilterUpcoming;

  /// No description provided for @tasksFilterOverdue.
  ///
  /// In en, this message translates to:
  /// **'Overdue'**
  String get tasksFilterOverdue;

  /// No description provided for @tomorrowLabel.
  ///
  /// In en, this message translates to:
  /// **'Tomorrow'**
  String get tomorrowLabel;

  /// No description provided for @emptyTasksToday.
  ///
  /// In en, this message translates to:
  /// **'Nothing scheduled for today.'**
  String get emptyTasksToday;

  /// No description provided for @emptyTasksUpcoming.
  ///
  /// In en, this message translates to:
  /// **'No upcoming tasks.'**
  String get emptyTasksUpcoming;

  /// No description provided for @emptyTasksOverdue.
  ///
  /// In en, this message translates to:
  /// **'No overdue tasks. You are all caught up.'**
  String get emptyTasksOverdue;

  /// No description provided for @taskOverdueByDays.
  ///
  /// In en, this message translates to:
  /// **'{days, plural, =1{1 day overdue} other{{days} days overdue}}'**
  String taskOverdueByDays(int days);

  /// No description provided for @certificateTitle.
  ///
  /// In en, this message translates to:
  /// **'Report of thorough examination'**
  String get certificateTitle;

  /// No description provided for @templateLabel.
  ///
  /// In en, this message translates to:
  /// **'Template {id}'**
  String templateLabel(String id);

  /// No description provided for @savedLabel.
  ///
  /// In en, this message translates to:
  /// **'Saved'**
  String get savedLabel;

  /// No description provided for @savingLabel.
  ///
  /// In en, this message translates to:
  /// **'Saving…'**
  String get savingLabel;

  /// No description provided for @stepOfTotalLabel.
  ///
  /// In en, this message translates to:
  /// **'Step {step} of {total}'**
  String stepOfTotalLabel(int step, int total);

  /// No description provided for @examinationDetailsTitle.
  ///
  /// In en, this message translates to:
  /// **'Examination details'**
  String get examinationDetailsTitle;

  /// No description provided for @itemInformationTitle.
  ///
  /// In en, this message translates to:
  /// **'Item information'**
  String get itemInformationTitle;

  /// No description provided for @examinationQuestionsTitle.
  ///
  /// In en, this message translates to:
  /// **'Examination questions'**
  String get examinationQuestionsTitle;

  /// No description provided for @defectsTitle.
  ///
  /// In en, this message translates to:
  /// **'Defects & remedial action'**
  String get defectsTitle;

  /// No description provided for @conclusionTitle.
  ///
  /// In en, this message translates to:
  /// **'Conclusion'**
  String get conclusionTitle;

  /// No description provided for @certificateNumberLabel.
  ///
  /// In en, this message translates to:
  /// **'Certificate number'**
  String get certificateNumberLabel;

  /// No description provided for @clientAndLocationLabel.
  ///
  /// In en, this message translates to:
  /// **'Client and location'**
  String get clientAndLocationLabel;

  /// No description provided for @clientRepresentativeLabel.
  ///
  /// In en, this message translates to:
  /// **'Client representative'**
  String get clientRepresentativeLabel;

  /// No description provided for @examinationDateLabel.
  ///
  /// In en, this message translates to:
  /// **'Examination date'**
  String get examinationDateLabel;

  /// No description provided for @nextExaminationDateLabel.
  ///
  /// In en, this message translates to:
  /// **'Next examination date'**
  String get nextExaminationDateLabel;

  /// No description provided for @standardOfInspectionLabel.
  ///
  /// In en, this message translates to:
  /// **'Standard of inspection'**
  String get standardOfInspectionLabel;

  /// No description provided for @testTypeLabel.
  ///
  /// In en, this message translates to:
  /// **'Test type'**
  String get testTypeLabel;

  /// No description provided for @selectDateHint.
  ///
  /// In en, this message translates to:
  /// **'Select date'**
  String get selectDateHint;

  /// No description provided for @inspectedItemLabel.
  ///
  /// In en, this message translates to:
  /// **'Inspected item'**
  String get inspectedItemLabel;

  /// No description provided for @manufacturerLabel.
  ///
  /// In en, this message translates to:
  /// **'Manufacturer'**
  String get manufacturerLabel;

  /// No description provided for @modelYearLabel.
  ///
  /// In en, this message translates to:
  /// **'Model / year of manufacture'**
  String get modelYearLabel;

  /// No description provided for @maxWorkingRateLabel.
  ///
  /// In en, this message translates to:
  /// **'Max. working rate'**
  String get maxWorkingRateLabel;

  /// No description provided for @serialNumberLabel.
  ///
  /// In en, this message translates to:
  /// **'Serial / chassis number'**
  String get serialNumberLabel;

  /// No description provided for @ownerIdLabel.
  ///
  /// In en, this message translates to:
  /// **'Owner ID'**
  String get ownerIdLabel;

  /// No description provided for @functionCheckLabel.
  ///
  /// In en, this message translates to:
  /// **'Function check'**
  String get functionCheckLabel;

  /// No description provided for @ndtLabel.
  ///
  /// In en, this message translates to:
  /// **'NDT'**
  String get ndtLabel;

  /// No description provided for @passOption.
  ///
  /// In en, this message translates to:
  /// **'Pass'**
  String get passOption;

  /// No description provided for @failOption.
  ///
  /// In en, this message translates to:
  /// **'Fail'**
  String get failOption;

  /// No description provided for @naOption.
  ///
  /// In en, this message translates to:
  /// **'N/A'**
  String get naOption;

  /// No description provided for @yesOption.
  ///
  /// In en, this message translates to:
  /// **'Yes'**
  String get yesOption;

  /// No description provided for @noOption.
  ///
  /// In en, this message translates to:
  /// **'No'**
  String get noOption;

  /// No description provided for @questionFirstExamination.
  ///
  /// In en, this message translates to:
  /// **'Is this the first examination after installation or assembly at a new site or location?'**
  String get questionFirstExamination;

  /// No description provided for @questionInstalledCorrectly.
  ///
  /// In en, this message translates to:
  /// **'Has the equipment been installed correctly?'**
  String get questionInstalledCorrectly;

  /// No description provided for @examinationCarriedOutLabel.
  ///
  /// In en, this message translates to:
  /// **'Was the examination carried out:'**
  String get examinationCarriedOutLabel;

  /// No description provided for @questionWithin6Months.
  ///
  /// In en, this message translates to:
  /// **'Within an interval of 6 months?'**
  String get questionWithin6Months;

  /// No description provided for @questionWithin12Months.
  ///
  /// In en, this message translates to:
  /// **'Within an interval of 12 months?'**
  String get questionWithin12Months;

  /// No description provided for @questionExaminationScheme.
  ///
  /// In en, this message translates to:
  /// **'In accordance with an examination scheme?'**
  String get questionExaminationScheme;

  /// No description provided for @questionExceptionalCircumstances.
  ///
  /// In en, this message translates to:
  /// **'After the occurrence of exceptional circumstances?'**
  String get questionExceptionalCircumstances;

  /// No description provided for @defectDescriptionLabel.
  ///
  /// In en, this message translates to:
  /// **'Any part found to have a defect which is or could become a danger to persons, and a description of the defect'**
  String get defectDescriptionLabel;

  /// No description provided for @defectDescriptionHint.
  ///
  /// In en, this message translates to:
  /// **'Leave empty if there is none'**
  String get defectDescriptionHint;

  /// No description provided for @defectsOptionalSubtitle.
  ///
  /// In en, this message translates to:
  /// **'Optional — only if something was found'**
  String get defectsOptionalSubtitle;

  /// No description provided for @existingDangerLabel.
  ///
  /// In en, this message translates to:
  /// **'Is the above an existing or imminent danger to persons? (a reportable defect)'**
  String get existingDangerLabel;

  /// No description provided for @futureDangerLabel.
  ///
  /// In en, this message translates to:
  /// **'Is the above a defect which is not yet, but could become, a danger to persons?'**
  String get futureDangerLabel;

  /// No description provided for @futureDangerByLabel.
  ///
  /// In en, this message translates to:
  /// **'If yes, the date by when'**
  String get futureDangerByLabel;

  /// No description provided for @repairsRequiredLabel.
  ///
  /// In en, this message translates to:
  /// **'Repair, renewal or alteration required to remedy the defect'**
  String get repairsRequiredLabel;

  /// No description provided for @testsCarriedOutLabel.
  ///
  /// In en, this message translates to:
  /// **'Tests carried out as part of the examination'**
  String get testsCarriedOutLabel;

  /// No description provided for @existingDangerShortLabel.
  ///
  /// In en, this message translates to:
  /// **'Existing danger'**
  String get existingDangerShortLabel;

  /// No description provided for @potentialDangerShortLabel.
  ///
  /// In en, this message translates to:
  /// **'Potential danger'**
  String get potentialDangerShortLabel;

  /// No description provided for @safeToOperateOption.
  ///
  /// In en, this message translates to:
  /// **'Safe to operate'**
  String get safeToOperateOption;

  /// No description provided for @safeWithConditionsOption.
  ///
  /// In en, this message translates to:
  /// **'Safe with conditions (fix in 14 days)'**
  String get safeWithConditionsOption;

  /// No description provided for @notSafeOption.
  ///
  /// In en, this message translates to:
  /// **'Not safe — out of service'**
  String get notSafeOption;

  /// No description provided for @previewAction.
  ///
  /// In en, this message translates to:
  /// **'Preview'**
  String get previewAction;

  /// No description provided for @submitToTechnicalManagerAction.
  ///
  /// In en, this message translates to:
  /// **'Submit to technical manager'**
  String get submitToTechnicalManagerAction;

  /// No description provided for @confirmSubmitCertificateTitle.
  ///
  /// In en, this message translates to:
  /// **'Submit this certificate?'**
  String get confirmSubmitCertificateTitle;

  /// No description provided for @confirmSubmitCertificateMessage.
  ///
  /// In en, this message translates to:
  /// **'Once submitted, you won\'t be able to make further changes.'**
  String get confirmSubmitCertificateMessage;

  /// No description provided for @submitAction.
  ///
  /// In en, this message translates to:
  /// **'Submit'**
  String get submitAction;

  /// No description provided for @certificateSubmittedMessage.
  ///
  /// In en, this message translates to:
  /// **'Certificate submitted.'**
  String get certificateSubmittedMessage;

  /// No description provided for @upcomingBadge.
  ///
  /// In en, this message translates to:
  /// **'Upcoming'**
  String get upcomingBadge;

  /// No description provided for @doneBadge.
  ///
  /// In en, this message translates to:
  /// **'Done'**
  String get doneBadge;

  /// No description provided for @returnedBadge.
  ///
  /// In en, this message translates to:
  /// **'Returned'**
  String get returnedBadge;

  /// No description provided for @myCertificatesTitle.
  ///
  /// In en, this message translates to:
  /// **'My certificates'**
  String get myCertificatesTitle;

  /// No description provided for @statNeedsAction.
  ///
  /// In en, this message translates to:
  /// **'Needs action'**
  String get statNeedsAction;

  /// No description provided for @statAwaitingReview.
  ///
  /// In en, this message translates to:
  /// **'Awaiting review'**
  String get statAwaitingReview;

  /// No description provided for @statSentThisMonth.
  ///
  /// In en, this message translates to:
  /// **'Sent this month'**
  String get statSentThisMonth;

  /// No description provided for @searchCertificatesHint.
  ///
  /// In en, this message translates to:
  /// **'Search client, equipment or cert no.'**
  String get searchCertificatesHint;

  /// No description provided for @filterActionTab.
  ///
  /// In en, this message translates to:
  /// **'Action'**
  String get filterActionTab;

  /// No description provided for @filterSubmittedTab.
  ///
  /// In en, this message translates to:
  /// **'Submitted'**
  String get filterSubmittedTab;

  /// No description provided for @filterSentTab.
  ///
  /// In en, this message translates to:
  /// **'Sent'**
  String get filterSentTab;

  /// No description provided for @notYetSubmittedLabel.
  ///
  /// In en, this message translates to:
  /// **'Not yet submitted'**
  String get notYetSubmittedLabel;

  /// No description provided for @draftPercentLabel.
  ///
  /// In en, this message translates to:
  /// **'Draft · {percent}%'**
  String draftPercentLabel(int percent);

  /// No description provided for @fixAndResubmitAction.
  ///
  /// In en, this message translates to:
  /// **'Fix & resubmit'**
  String get fixAndResubmitAction;

  /// No description provided for @viewPdfAction.
  ///
  /// In en, this message translates to:
  /// **'View PDF'**
  String get viewPdfAction;

  /// No description provided for @submittedRelativeLabel.
  ///
  /// In en, this message translates to:
  /// **'Submitted {relative}'**
  String submittedRelativeLabel(String relative);

  /// No description provided for @emptyCertificatesList.
  ///
  /// In en, this message translates to:
  /// **'No certificates match.'**
  String get emptyCertificatesList;

  /// No description provided for @certificateNotFound.
  ///
  /// In en, this message translates to:
  /// **'Certificate details aren\'t available.'**
  String get certificateNotFound;

  /// No description provided for @reviewNavReview.
  ///
  /// In en, this message translates to:
  /// **'Review'**
  String get reviewNavReview;

  /// No description provided for @reviewNavSent.
  ///
  /// In en, this message translates to:
  /// **'Sent'**
  String get reviewNavSent;

  /// No description provided for @reviewQueueTitle.
  ///
  /// In en, this message translates to:
  /// **'Certificates to review'**
  String get reviewQueueTitle;

  /// No description provided for @statToReview.
  ///
  /// In en, this message translates to:
  /// **'To review'**
  String get statToReview;

  /// No description provided for @statReturned.
  ///
  /// In en, this message translates to:
  /// **'Returned'**
  String get statReturned;

  /// No description provided for @waitingForSignatureTitle.
  ///
  /// In en, this message translates to:
  /// **'Waiting for your signature'**
  String get waitingForSignatureTitle;

  /// No description provided for @reviewAndSignAction.
  ///
  /// In en, this message translates to:
  /// **'Review & sign'**
  String get reviewAndSignAction;

  /// No description provided for @byInspectorWhenLabel.
  ///
  /// In en, this message translates to:
  /// **'By {name} · {when}'**
  String byInspectorWhenLabel(String name, String when);

  /// No description provided for @todayAtTime.
  ///
  /// In en, this message translates to:
  /// **'today {time}'**
  String todayAtTime(String time);

  /// No description provided for @yesterdayLabel.
  ///
  /// In en, this message translates to:
  /// **'yesterday'**
  String get yesterdayLabel;

  /// No description provided for @returnedToInspector.
  ///
  /// In en, this message translates to:
  /// **'Returned to inspector'**
  String get returnedToInspector;

  /// No description provided for @returnedToInspectorNote.
  ///
  /// In en, this message translates to:
  /// **'Returned to inspector — {note}'**
  String returnedToInspectorNote(String note);

  /// No description provided for @emptyReviewQueue.
  ///
  /// In en, this message translates to:
  /// **'Nothing is waiting for your signature.'**
  String get emptyReviewQueue;

  /// No description provided for @sentCertificatesTitle.
  ///
  /// In en, this message translates to:
  /// **'Sent certificates'**
  String get sentCertificatesTitle;

  /// No description provided for @emptySentCertificates.
  ///
  /// In en, this message translates to:
  /// **'No certificates have been sent yet.'**
  String get emptySentCertificates;

  /// No description provided for @sentToLabel.
  ///
  /// In en, this message translates to:
  /// **'Sent to {email}'**
  String sentToLabel(String email);

  /// No description provided for @reviewBadgeSafe.
  ///
  /// In en, this message translates to:
  /// **'Safe'**
  String get reviewBadgeSafe;

  /// No description provided for @reviewBadgeConditions.
  ///
  /// In en, this message translates to:
  /// **'With conditions'**
  String get reviewBadgeConditions;

  /// No description provided for @reviewBadgeNotSafe.
  ///
  /// In en, this message translates to:
  /// **'Not safe'**
  String get reviewBadgeNotSafe;

  /// No description provided for @reviewResultSafe.
  ///
  /// In en, this message translates to:
  /// **'Safe to operate'**
  String get reviewResultSafe;

  /// No description provided for @reviewResultConditions.
  ///
  /// In en, this message translates to:
  /// **'Safe with conditions'**
  String get reviewResultConditions;

  /// No description provided for @reviewResultNotSafe.
  ///
  /// In en, this message translates to:
  /// **'Not safe — out of service'**
  String get reviewResultNotSafe;

  /// No description provided for @reviewCertificateTitle.
  ///
  /// In en, this message translates to:
  /// **'Review certificate'**
  String get reviewCertificateTitle;

  /// No description provided for @awaitingYouBadge.
  ///
  /// In en, this message translates to:
  /// **'Awaiting you'**
  String get awaitingYouBadge;

  /// No description provided for @reviewSummaryTitle.
  ///
  /// In en, this message translates to:
  /// **'Summary'**
  String get reviewSummaryTitle;

  /// No description provided for @viewFullPdfAction.
  ///
  /// In en, this message translates to:
  /// **'View full PDF'**
  String get viewFullPdfAction;

  /// No description provided for @inspectedLabel.
  ///
  /// In en, this message translates to:
  /// **'Inspected'**
  String get inspectedLabel;

  /// No description provided for @nextDueLabel.
  ///
  /// In en, this message translates to:
  /// **'Next due'**
  String get nextDueLabel;

  /// No description provided for @yourCommentsLabel.
  ///
  /// In en, this message translates to:
  /// **'Your comments'**
  String get yourCommentsLabel;

  /// No description provided for @reviewCommentsHint.
  ///
  /// In en, this message translates to:
  /// **'Add a note for the record (optional)'**
  String get reviewCommentsHint;

  /// No description provided for @signHereTitle.
  ///
  /// In en, this message translates to:
  /// **'Sign here'**
  String get signHereTitle;

  /// No description provided for @clearAction.
  ///
  /// In en, this message translates to:
  /// **'Clear'**
  String get clearAction;

  /// No description provided for @savedSignatureTitle.
  ///
  /// In en, this message translates to:
  /// **'Your signature'**
  String get savedSignatureTitle;

  /// No description provided for @savedSignatureHint.
  ///
  /// In en, this message translates to:
  /// **'Used on every certificate you approve. Change it in your Profile.'**
  String get savedSignatureHint;

  /// No description provided for @rememberSignatureLabel.
  ///
  /// In en, this message translates to:
  /// **'Remember my signature for next time'**
  String get rememberSignatureLabel;

  /// No description provided for @signatureSheetTitle.
  ///
  /// In en, this message translates to:
  /// **'Draw your signature'**
  String get signatureSheetTitle;

  /// No description provided for @signatureSheetHint.
  ///
  /// In en, this message translates to:
  /// **'Saved to your profile and used every time you approve a certificate.'**
  String get signatureSheetHint;

  /// No description provided for @sendSignedPdfToLabel.
  ///
  /// In en, this message translates to:
  /// **'Send signed PDF to'**
  String get sendSignedPdfToLabel;

  /// No description provided for @returnAction.
  ///
  /// In en, this message translates to:
  /// **'Return'**
  String get returnAction;

  /// No description provided for @approveAndSendAction.
  ///
  /// In en, this message translates to:
  /// **'Approve & send to client'**
  String get approveAndSendAction;

  /// No description provided for @approveRequirementsHint.
  ///
  /// In en, this message translates to:
  /// **'Add your signature and a valid email to approve.'**
  String get approveRequirementsHint;

  /// No description provided for @confirmApproveTitle.
  ///
  /// In en, this message translates to:
  /// **'Approve this certificate?'**
  String get confirmApproveTitle;

  /// No description provided for @confirmApproveMessage.
  ///
  /// In en, this message translates to:
  /// **'It will be signed as {name} and marked as sent to {email}.'**
  String confirmApproveMessage(String name, String email);

  /// No description provided for @approveConfirmAction.
  ///
  /// In en, this message translates to:
  /// **'Approve'**
  String get approveConfirmAction;

  /// No description provided for @returnToInspectorTitle.
  ///
  /// In en, this message translates to:
  /// **'Return to inspector'**
  String get returnToInspectorTitle;

  /// No description provided for @returnReasonLabel.
  ///
  /// In en, this message translates to:
  /// **'What needs to change?'**
  String get returnReasonLabel;

  /// No description provided for @returnReasonHint.
  ///
  /// In en, this message translates to:
  /// **'e.g. Add photos of the landing door interlock'**
  String get returnReasonHint;

  /// No description provided for @returnReasonRequired.
  ///
  /// In en, this message translates to:
  /// **'Add a reason so the inspector knows what to fix.'**
  String get returnReasonRequired;

  /// No description provided for @returnConfirmAction.
  ///
  /// In en, this message translates to:
  /// **'Return'**
  String get returnConfirmAction;

  /// No description provided for @certificateApprovedMessage.
  ///
  /// In en, this message translates to:
  /// **'Certificate approved and signed.'**
  String get certificateApprovedMessage;

  /// No description provided for @certificateReturnedMessage.
  ///
  /// In en, this message translates to:
  /// **'Certificate returned to the inspector.'**
  String get certificateReturnedMessage;

  /// No description provided for @dateRangeTitle.
  ///
  /// In en, this message translates to:
  /// **'Select dates'**
  String get dateRangeTitle;

  /// No description provided for @dateRangeStartLabel.
  ///
  /// In en, this message translates to:
  /// **'Start'**
  String get dateRangeStartLabel;

  /// No description provided for @dateRangeEndLabel.
  ///
  /// In en, this message translates to:
  /// **'End'**
  String get dateRangeEndLabel;

  /// No description provided for @dateRangeSelectHint.
  ///
  /// In en, this message translates to:
  /// **'Tap a start date.'**
  String get dateRangeSelectHint;

  /// No description provided for @dateRangePickEndHint.
  ///
  /// In en, this message translates to:
  /// **'Tap an end date, or confirm for a single day.'**
  String get dateRangePickEndHint;

  /// No description provided for @dateRangeReadyHint.
  ///
  /// In en, this message translates to:
  /// **'Ready to confirm.'**
  String get dateRangeReadyHint;

  /// No description provided for @previousMonthTooltip.
  ///
  /// In en, this message translates to:
  /// **'Previous month'**
  String get previousMonthTooltip;

  /// No description provided for @nextMonthTooltip.
  ///
  /// In en, this message translates to:
  /// **'Next month'**
  String get nextMonthTooltip;

  /// No description provided for @confirmAction.
  ///
  /// In en, this message translates to:
  /// **'Confirm'**
  String get confirmAction;

  /// No description provided for @downloadPdfAction.
  ///
  /// In en, this message translates to:
  /// **'Download PDF'**
  String get downloadPdfAction;

  /// No description provided for @preparingPdfMessage.
  ///
  /// In en, this message translates to:
  /// **'Preparing your PDF…'**
  String get preparingPdfMessage;

  /// No description provided for @pdfPreviewFailedMessage.
  ///
  /// In en, this message translates to:
  /// **'Couldn\'t prepare the PDF.'**
  String get pdfPreviewFailedMessage;

  /// No description provided for @pdfDownloadFailedMessage.
  ///
  /// In en, this message translates to:
  /// **'Couldn\'t download the PDF. Try again.'**
  String get pdfDownloadFailedMessage;

  /// No description provided for @timesheetTitle.
  ///
  /// In en, this message translates to:
  /// **'Timesheet'**
  String get timesheetTitle;

  /// No description provided for @timesheetTotalPriceLabel.
  ///
  /// In en, this message translates to:
  /// **'Total'**
  String get timesheetTotalPriceLabel;

  /// No description provided for @timesheetTimeLoggedLabel.
  ///
  /// In en, this message translates to:
  /// **'Time logged'**
  String get timesheetTimeLoggedLabel;

  /// No description provided for @timesheetLoggedProgress.
  ///
  /// In en, this message translates to:
  /// **'{logged} of {total} tasks logged'**
  String timesheetLoggedProgress(int logged, int total);

  /// No description provided for @timesheetEmptyMonth.
  ///
  /// In en, this message translates to:
  /// **'No accepted tasks this month.'**
  String get timesheetEmptyMonth;

  /// No description provided for @timeSpentLabel.
  ///
  /// In en, this message translates to:
  /// **'Time spent'**
  String get timeSpentLabel;

  /// No description provided for @priceLabel.
  ///
  /// In en, this message translates to:
  /// **'Price'**
  String get priceLabel;

  /// No description provided for @addTimeAndPriceAction.
  ///
  /// In en, this message translates to:
  /// **'Log time & costs'**
  String get addTimeAndPriceAction;

  /// No description provided for @timesheetSheetTitle.
  ///
  /// In en, this message translates to:
  /// **'Time & costs'**
  String get timesheetSheetTitle;

  /// No description provided for @hoursLabel.
  ///
  /// In en, this message translates to:
  /// **'Hours'**
  String get hoursLabel;

  /// No description provided for @minutesLabel.
  ///
  /// In en, this message translates to:
  /// **'Minutes'**
  String get minutesLabel;

  /// No description provided for @errorMinutesRange.
  ///
  /// In en, this message translates to:
  /// **'Minutes must be between 0 and 59.'**
  String get errorMinutesRange;

  /// No description provided for @errorTimeOrPriceRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter the time spent or a transportation cost.'**
  String get errorTimeOrPriceRequired;

  /// No description provided for @timesheetSavedMessage.
  ///
  /// In en, this message translates to:
  /// **'Saved to your timesheet.'**
  String get timesheetSavedMessage;

  /// No description provided for @timesheetPermissionDenied.
  ///
  /// In en, this message translates to:
  /// **'You don\'t have access to this timesheet.'**
  String get timesheetPermissionDenied;

  /// No description provided for @durationHoursMinutes.
  ///
  /// In en, this message translates to:
  /// **'{hours}h {minutes}m'**
  String durationHoursMinutes(int hours, int minutes);

  /// No description provided for @durationHours.
  ///
  /// In en, this message translates to:
  /// **'{hours}h'**
  String durationHours(int hours);

  /// No description provided for @durationMinutes.
  ///
  /// In en, this message translates to:
  /// **'{minutes}m'**
  String durationMinutes(int minutes);

  /// No description provided for @navTimesheets.
  ///
  /// In en, this message translates to:
  /// **'Timesheets'**
  String get navTimesheets;

  /// No description provided for @timesheetReviewTitle.
  ///
  /// In en, this message translates to:
  /// **'Timesheets'**
  String get timesheetReviewTitle;

  /// No description provided for @timesheetAwaitingApprovalLabel.
  ///
  /// In en, this message translates to:
  /// **'Awaiting approval'**
  String get timesheetAwaitingApprovalLabel;

  /// No description provided for @timesheetPendingValueLabel.
  ///
  /// In en, this message translates to:
  /// **'Pending value'**
  String get timesheetPendingValueLabel;

  /// No description provided for @timesheetFilterPending.
  ///
  /// In en, this message translates to:
  /// **'Pending'**
  String get timesheetFilterPending;

  /// No description provided for @timesheetStatusPending.
  ///
  /// In en, this message translates to:
  /// **'Awaiting approval'**
  String get timesheetStatusPending;

  /// No description provided for @timesheetStatusApproved.
  ///
  /// In en, this message translates to:
  /// **'Approved'**
  String get timesheetStatusApproved;

  /// No description provided for @timesheetStatusReturned.
  ///
  /// In en, this message translates to:
  /// **'Returned'**
  String get timesheetStatusReturned;

  /// No description provided for @timesheetApprovedBy.
  ///
  /// In en, this message translates to:
  /// **'Approved by {name}'**
  String timesheetApprovedBy(String name);

  /// No description provided for @timesheetReturnedBy.
  ///
  /// In en, this message translates to:
  /// **'Returned by {name}'**
  String timesheetReturnedBy(String name);

  /// No description provided for @timesheetLockedTooltip.
  ///
  /// In en, this message translates to:
  /// **'Approved — can\'t be changed'**
  String get timesheetLockedTooltip;

  /// No description provided for @timesheetApproveTitle.
  ///
  /// In en, this message translates to:
  /// **'Approve this entry?'**
  String get timesheetApproveTitle;

  /// No description provided for @timesheetApproveMessage.
  ///
  /// In en, this message translates to:
  /// **'{name} logged {time} at {price}. Once approved it can\'t be changed.'**
  String timesheetApproveMessage(String name, String time, String price);

  /// No description provided for @timesheetApprovedMessage.
  ///
  /// In en, this message translates to:
  /// **'Entry approved.'**
  String get timesheetApprovedMessage;

  /// No description provided for @timesheetReturnedMessage.
  ///
  /// In en, this message translates to:
  /// **'Returned to the inspector.'**
  String get timesheetReturnedMessage;

  /// No description provided for @timesheetReturnReasonHint.
  ///
  /// In en, this message translates to:
  /// **'e.g. This price is too high for a half-day job'**
  String get timesheetReturnReasonHint;

  /// No description provided for @timesheetReviewEmptyPending.
  ///
  /// In en, this message translates to:
  /// **'Nothing is waiting for approval.'**
  String get timesheetReviewEmptyPending;

  /// No description provided for @timesheetReviewEmptyOther.
  ///
  /// In en, this message translates to:
  /// **'No entries here yet.'**
  String get timesheetReviewEmptyOther;

  /// No description provided for @timesheetActivitiesLabel.
  ///
  /// In en, this message translates to:
  /// **'How the time was spent'**
  String get timesheetActivitiesLabel;

  /// No description provided for @timesheetActivityInspection.
  ///
  /// In en, this message translates to:
  /// **'Inspection & load testing'**
  String get timesheetActivityInspection;

  /// No description provided for @timesheetExpensesLabel.
  ///
  /// In en, this message translates to:
  /// **'Transportation costs'**
  String get timesheetExpensesLabel;

  /// No description provided for @timesheetExpenseInternalTransport.
  ///
  /// In en, this message translates to:
  /// **'Internal transportation'**
  String get timesheetExpenseInternalTransport;

  /// No description provided for @timesheetExpenseExternalTransport.
  ///
  /// In en, this message translates to:
  /// **'External transportation'**
  String get timesheetExpenseExternalTransport;

  /// No description provided for @timesheetTransportationLabel.
  ///
  /// In en, this message translates to:
  /// **'Transportation'**
  String get timesheetTransportationLabel;

  /// No description provided for @timesheetIncludingTransportation.
  ///
  /// In en, this message translates to:
  /// **'Incl. {amount} transportation'**
  String timesheetIncludingTransportation(String amount);

  /// No description provided for @timesheetApproveMessageWithTransport.
  ///
  /// In en, this message translates to:
  /// **'{name} logged {time} at {price}, plus {transport} transportation. Once approved it can\'t be changed.'**
  String timesheetApproveMessageWithTransport(
    String name,
    String time,
    String price,
    String transport,
  );

  /// No description provided for @timesheetActivityReport.
  ///
  /// In en, this message translates to:
  /// **'Report preparation'**
  String get timesheetActivityReport;

  /// No description provided for @timesheetActivityWaiting.
  ///
  /// In en, this message translates to:
  /// **'Waiting'**
  String get timesheetActivityWaiting;

  /// No description provided for @timesheetActivityUnpaidBreak.
  ///
  /// In en, this message translates to:
  /// **'Unpaid break'**
  String get timesheetActivityUnpaidBreak;

  /// No description provided for @timesheetElapsedLabel.
  ///
  /// In en, this message translates to:
  /// **'Total elapsed'**
  String get timesheetElapsedLabel;

  /// No description provided for @timesheetNetTimeLabel.
  ///
  /// In en, this message translates to:
  /// **'Net working time'**
  String get timesheetNetTimeLabel;

  /// No description provided for @hourlyRateLabel.
  ///
  /// In en, this message translates to:
  /// **'Hourly rate'**
  String get hourlyRateLabel;

  /// No description provided for @hourlyRatePerHour.
  ///
  /// In en, this message translates to:
  /// **'{rate} / hour'**
  String hourlyRatePerHour(String rate);

  /// No description provided for @hourlyRateSetPrompt.
  ///
  /// In en, this message translates to:
  /// **'Set your hourly rate'**
  String get hourlyRateSetPrompt;

  /// No description provided for @hourlyRateSheetTitle.
  ///
  /// In en, this message translates to:
  /// **'Hourly rate'**
  String get hourlyRateSheetTitle;

  /// No description provided for @hourlyRateSheetHint.
  ///
  /// In en, this message translates to:
  /// **'Your price on each task is worked out from this: the net working time × your hourly rate. It applies to entries you save from now on — entries already logged keep the rate they were saved with.'**
  String get hourlyRateSheetHint;

  /// No description provided for @errorHourlyRateRequired.
  ///
  /// In en, this message translates to:
  /// **'Enter an hourly rate.'**
  String get errorHourlyRateRequired;

  /// No description provided for @hourlyRateSavedMessage.
  ///
  /// In en, this message translates to:
  /// **'Hourly rate saved.'**
  String get hourlyRateSavedMessage;

  /// No description provided for @timesheetPriceFormula.
  ///
  /// In en, this message translates to:
  /// **'{time} × {rate} per hour'**
  String timesheetPriceFormula(String time, String rate);

  /// No description provided for @timesheetRateMissing.
  ///
  /// In en, this message translates to:
  /// **'Set your hourly rate on the Timesheet tab to price this entry.'**
  String get timesheetRateMissing;

  /// No description provided for @timesheetShowDetails.
  ///
  /// In en, this message translates to:
  /// **'Details'**
  String get timesheetShowDetails;

  /// No description provided for @timesheetHideDetails.
  ///
  /// In en, this message translates to:
  /// **'Hide details'**
  String get timesheetHideDetails;

  /// No description provided for @timesheetLockedLabel.
  ///
  /// In en, this message translates to:
  /// **'Locked'**
  String get timesheetLockedLabel;

  /// No description provided for @timesheetExcludingBreak.
  ///
  /// In en, this message translates to:
  /// **'Excl. {duration} break'**
  String timesheetExcludingBreak(String duration);

  /// No description provided for @timesheetPricePlusTransport.
  ///
  /// In en, this message translates to:
  /// **'Price {price}\n+ {transport} transport'**
  String timesheetPricePlusTransport(String price, String transport);
}

class _AppLocalizationsDelegate
    extends LocalizationsDelegate<AppLocalizations> {
  const _AppLocalizationsDelegate();

  @override
  Future<AppLocalizations> load(Locale locale) {
    return SynchronousFuture<AppLocalizations>(lookupAppLocalizations(locale));
  }

  @override
  bool isSupported(Locale locale) =>
      <String>['ar', 'en'].contains(locale.languageCode);

  @override
  bool shouldReload(_AppLocalizationsDelegate old) => false;
}

AppLocalizations lookupAppLocalizations(Locale locale) {
  // Lookup logic when only language code is specified.
  switch (locale.languageCode) {
    case 'ar':
      return AppLocalizationsAr();
    case 'en':
      return AppLocalizationsEn();
  }

  throw FlutterError(
    'AppLocalizations.delegate failed to load unsupported locale "$locale". This is likely '
    'an issue with the localizations generation tool. Please file an issue '
    'on GitHub with a reproducible sample app and the gen-l10n configuration '
    'that was used.',
  );
}
