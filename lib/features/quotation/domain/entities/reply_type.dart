/// The coordinator's three ways to reply to a request (BR-03.1).
enum ReplyType {
  accept('accept'),
  offer('offer'),
  reject('reject');

  final String value;

  const ReplyType(this.value);

  static ReplyType? fromValue(Object? value) {
    for (final type in values) {
      if (type.value == value) return type;
    }
    return null;
  }
}
