class Validators {
  Validators._();

  static final _email = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');

  /// A loose shape check; the server decides whether the address exists.
  static bool isEmail(String value) => _email.hasMatch(value.trim());

  /// The API's password rules: 12 or more characters with an upper-case
  /// letter, a lower-case letter, a digit and a symbol. The server still has
  /// the last word.
  static bool isStrongPassword(String value) =>
      value.length >= 12 &&
      value.contains(RegExp(r'[A-Z]')) &&
      value.contains(RegExp(r'[a-z]')) &&
      value.contains(RegExp(r'[0-9]')) &&
      value.contains(RegExp(r'[^A-Za-z0-9]'));
}
