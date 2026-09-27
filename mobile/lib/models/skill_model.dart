import '../helper/json_coercion.dart';

/// A `Skills` row as returned by `GET /api/categories/skills`:
/// `{id, name, category_id, category, category_name, is_custom}`.
///
/// The Find Projects drawer only needs `id`/`name`, but the extra fields are kept so
/// the same model can parse the full response shape without coercion surprises.
class Skill {
  final int id;
  final String name;

  const Skill({required this.id, required this.name});

  factory Skill.fromJson(Map json) => Skill(
        id: asInt(json['id']),
        name: (json['name'] ?? '').toString(),
      );
}