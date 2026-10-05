import 'dart:convert';

CategoryModel categoryModelFromJson(String str) =>
    CategoryModel.fromJson(json.decode(str));

String categoryModelToJson(CategoryModel data) => json.encode(data.toJson());

/// `GET /api/categories` (`CategoriesController`).
///
/// The server has **no sub-category concept at all** -- there is no `SubCategory`
/// entity, route or DTO. `Category` is a flat `{id, name}` record, and the only two
/// things you can nest under it are the skills attached to a project. The template
/// model read `data` / `links.next` and a `sub_categories` array; none of those keys
/// exist on this API, so `data` always fell through to `[]` and the dropdown rendered
/// a permanent "No results found".
///
/// Two shapes are legal, and which one you get depends on the request:
///   * `?no_pagination=true` -> a bare `[{id, name}, ...]` (what the dropdowns ask
///     for, and what the web client asks for in `categoryApi.js`);
///   * otherwise -> `PagedResponse<CategoryResponse>`:
///     `{results, count, page, page_size, total_pages, next, previous}`.
class CategoryModel {
  List<Category> data;
  String? next;

  CategoryModel({List<Category>? data, this.next}) : data = data ?? <Category>[];

  factory CategoryModel.fromJson(dynamic json) {
    // Bare array: `?no_pagination=true`.
    if (json is List) {
      return CategoryModel(
        data: json
            .whereType<Map>()
            .map((e) => Category.fromJson(e.cast<String, dynamic>()))
            .toList(),
      );
    }
    if (json is! Map) return CategoryModel();
    final map = json.cast<String, dynamic>();
    // Paged envelope, or the Laravel-ish `data` array the template expected.
    final rows = map['results'] ?? map['data'];
    return CategoryModel(
      data: rows is List
          ? rows
              .whereType<Map>()
              .map((e) => Category.fromJson(e.cast<String, dynamic>()))
              .toList()
          : <Category>[],
      next: map['next']?.toString(),
    );
  }

  Map<String, dynamic> toJson() => {
        'data': data.map((e) => e.toJson()).toList(),
        'next': next,
      };
}

class Category {
  int? id;
  String? name;

  Category({this.id, this.name});

  factory Category.fromJson(Map<String, dynamic> json) => Category(
        // The API sends `id` as a number; the template's ids were strings, so
        // accept either rather than ending up with a "1" that never matches the
        // `?category=1` the job-list endpoint binds to an int.
        id: switch (json['id']) {
          final int v => v,
          final num v => v.toInt(),
          final String v => int.tryParse(v),
          _ => null,
        },
        name: (json['name'] ?? json['category'])?.toString(),
      );

  /// Value equality. Without this, every sheet open refetches a fresh list, so the
  /// `Category` the user re-picks is a different instance from the one held in the
  /// notifier. `HomeDrawerViewModel.setFilters` compares with `==` to decide whether
  /// anything changed, and the dropdown early-returns when the tapped row is already
  /// selected -- both silently never matched, so re-picking the same category fired a
  /// pointless refetch.
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is Category && other.id == id && other.name == name);

  @override
  int get hashCode => Object.hash(id, name);

  Map<String, dynamic> toJson() => {'id': id, 'name': name};
}
