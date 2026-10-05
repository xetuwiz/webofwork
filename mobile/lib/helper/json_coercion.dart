/// Coercion helpers for reading this API's JSON.
///
/// Every numeric and enum-ish value in a WebOfWork payload is unreliable in a
/// specific, known way, so each is coerced once here instead of at every call
/// site:
///
///  * **`decimal` arrives quoted.** `Program.cs` registers
///    `DecimalAsStringJsonConverter`, whose `Read` calls `reader.GetString()` --
///    which *throws* on a bare number. So `total_amount` is `"250.00"`, not
///    `250.00`. Assigning it straight to a `num` is a runtime `TypeError`; that
///    is exactly what the old project-details apply sheet did, crashing the
///    moment a package tier was tapped.
///  * **Enums arrive as snake_case strings.** `JsonStringEnumConverter(SnakeCaseLower)`
///    means `ContractStatus.Active` serialises as `"active"`, never `1`.
///  * **`DateTime?` is a nullable ISO string**, not always present.
///  * **Nullable uuid columns** are `null` or a raw uuid string, never `""`.
///
/// Each helper therefore accepts *either* representation, so a payload that
/// happens to carry a real number is not silently coerced to 0.
library;

/// Reads a `decimal`/`double` sent as a JSON string, a number, or null.
double asDouble(dynamic value) {
  if (value is num) return value.toDouble();
  return double.tryParse((value ?? '').toString().trim()) ?? 0;
}

/// Reads an `int` sent as a JSON string, a number, or null.
///
/// Returns **0** for absent input, so only use it for ids that are always
/// present. For a genuinely optional id use [asIntOrNull].
int asInt(dynamic value) {
  if (value is num) return value.toInt();
  return int.tryParse((value ?? '').toString().trim()) ?? 0;
}

/// Reads an optional `int`, returning **null** for absent or unparseable input.
///
/// The distinction from [asInt] matters wherever null and zero mean different
/// things. `AppNotification.relatedEntityId` is the case in point: it is
/// `int?` and the tile checks `relatedEntityId != null` to decide whether the row
/// is tappable. Coerced with [asInt] it was *always* non-null -- 0 for a
/// notification with no related entity -- so every target-less row was rendered
/// as tappable and, for a `contract` notification, pushed
/// `MyContractDetailsView` with id 0.
int? asIntOrNull(dynamic value) {
  if (value == null) return null;
  if (value is int) return value;
  if (value is num) return value.toInt();
  if (value is String) {
    final raw = value.trim();
    if (raw.isEmpty) return null;
    return int.tryParse(raw);
  }
  return null;
}

/// Reads a bool sent as `true`, `"true"`, `"1"`, or `1`.
///
/// Some flags arrive as real bools and some as strings, so a bare `== true`
/// comparison silently drops half of them.
bool asBool(dynamic value) {
  if (value is bool) return value;
  if (value is num) return value != 0;
  switch (value?.toString().trim().toLowerCase()) {
    case 'true':
    case '1':
    case 'yes':
      return true;
    default:
      return false;
  }
}

/// Reads a list of strings, tolerating a newline-delimited blob or null.
List<String> asStringList(dynamic value) {
  if (value is List) {
    return value.map((e) => e.toString().trim()).where((e) => e.isNotEmpty).toList();
  }
  if (value is String && value.trim().isNotEmpty) {
    return value.split('\n').map((e) => e.trim()).where((e) => e.isNotEmpty).toList();
  }
  return const [];
}

/// Reads an ISO-8601 timestamp, returning null for absent or unparseable input.
DateTime? asDateOrNull(dynamic value) {
  if (value == null) return null;
  if (value is DateTime) return value;
  final raw = value.toString().trim();
  if (raw.isEmpty) return null;
  return DateTime.tryParse(raw);
}

/// Reads a non-empty string, collapsing absent/blank to null.
///
/// Keeps nullable uuid columns (`package_id`, `client_user_id`) from arriving as
/// `""`, which reads as "present but empty" at every call site.
String? asOptString(dynamic value) {
  if (value == null) return null;
  final s = value.toString().trim();
  return s.isEmpty ? null : s;
}

/// Maps a JSON array through [parse], skipping non-object rows so one malformed
/// entry cannot empty the whole list.
List<T> asObjectList<T>(dynamic rows, T Function(Map) parse) {
  if (rows is! List) return <T>[];
  return rows.whereType<Map>().map(parse).toList(growable: false);
}

/// Reads a nested JSON object, returning null for absent or non-object input.
///
/// For the `object?`-typed columns on this API -- `contract_info` on an
/// invitation, `reviews`/`payment_settings` on a profile. `dynamic` there means
/// the value can legitimately be a `Map`, a `List`, or `null`, and an unguarded
/// `Map<String, dynamic>.from(x)` throws a `TypeError` on the latter two.
Map<String, dynamic>? asObjectMap(dynamic value) {
  if (value is Map) return Map<String, dynamic>.from(value);
  return null;
}
