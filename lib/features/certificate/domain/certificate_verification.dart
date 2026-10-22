import 'dart:math';

/// Where the management dashboard is served. A client who scans a
/// certificate's QR lands on its public verification page here.
const certificateVerifyBaseUrl = 'https://integrity-manage.web.app';

/// The link a certificate's QR carries: the dashboard's public page for the
/// certificate of [requestId] (the certificate's document id), with its
/// [verificationCode] so the page can tell a genuine QR from a stale one.
String certificateVerifyUrl({required String requestId, required String verificationCode}) =>
    '$certificateVerifyBaseUrl/verify/${Uri.encodeComponent(requestId)}'
    '?code=${Uri.encodeQueryComponent(verificationCode)}';

/// Letters and digits with the look-alikes (0/O, 1/I) left out, so a code
/// read off a printout is hard to mistype.
const _alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

/// A fresh, unguessable code that identifies one approved certificate —
/// e.g. "K7QM-2XPA-9WDF". It goes into the certificate's QR code.
String newVerificationCode() {
  final random = Random.secure();
  final chars = [for (var i = 0; i < 12; i++) _alphabet[random.nextInt(_alphabet.length)]];
  return [
    chars.sublist(0, 4).join(),
    chars.sublist(4, 8).join(),
    chars.sublist(8, 12).join(),
  ].join('-');
}
