import 'package:equatable/equatable.dart';

/// The pages of a list loaded so far, for any paged endpoint. [page] is the
/// last page loaded (0 before the first), and [hasMore] says whether asking
/// for [nextPage] can return anything.
class Paginated<T> extends Equatable {
  final List<T> items;
  final int page;
  final bool hasMore;

  const Paginated({this.items = const [], this.page = 0, this.hasMore = true});

  /// One page as an endpoint returned it. The endpoint reports no total, so a
  /// page that comes back full means there may be another. Pass [rawCount]
  /// when [items] was filtered after the fetch, so that dropping rows
  /// doesn't read as the last page.
  factory Paginated.page({
    required List<T> items,
    required int page,
    required int pageSize,
    int? rawCount,
  }) => Paginated(
    items: items,
    page: page,
    hasMore: (rawCount ?? items.length) >= pageSize,
  );

  int get nextPage => page + 1;

  bool get isEmpty => items.isEmpty;

  /// This list followed by [next]. Rows of [next] whose [keyOf] is already
  /// here are dropped, so a row that moved between pages can't appear twice.
  Paginated<T> merge(Paginated<T> next, {Object Function(T item)? keyOf}) {
    final seen = keyOf == null ? null : {for (final item in items) keyOf(item)};
    return Paginated(
      items: [
        ...items,
        for (final item in next.items)
          if (seen == null || !seen.contains(keyOf!(item))) item,
      ],
      page: next.page,
      hasMore: next.hasMore,
    );
  }

  /// A copy of the loaded rows with [change] applied, keeping the paging.
  Paginated<T> mapItems(List<T> Function(List<T> items) change) =>
      Paginated(items: change(items), page: page, hasMore: hasMore);

  @override
  List<Object?> get props => [items, page, hasMore];
}
