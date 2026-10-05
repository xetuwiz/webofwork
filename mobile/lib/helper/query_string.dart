/// Query-string encoding for this API.
///
/// ## Why this exists
///
/// Dart's `Uri.encodeQueryComponent` encodes a space as **`+`**, following the
/// HTML form-encoding convention. ASP.NET Core does not read it that way.
///
/// `QueryHelpers.ParseQuery` (what backs `Request.Query`) decodes with
/// `Uri.UnescapeDataString`, which only resolves `%XX` sequences -- it leaves a
/// literal `+` alone. PHP, Python's `urllib`, and browsers all treat `+` as a
/// space in a query string; ASP.NET Core does not.
///
/// The result on Find Projects was silent and total: typing **"web design"** sent
/// `?search=web+design`, the server searched for the literal string
/// `"web+design"`, and matched **zero projects** -- with a 200 and an empty list,
/// so nothing looked broken. `Uri.decodeQueryComponent` on the client has the
/// mirror-image bug, so a test that round-tripped through it would have passed.
///
/// Percent-encoding the space as `%20` is the one form every one of those
/// decoders agrees on.
library;

/// Percent-encodes [value] for use in a query string, so a space becomes `%20`
/// rather than `+`.
///
/// Use this instead of `Uri.encodeQueryComponent` for **any** value sent to
/// WebOfWork, not just free text. It is a drop-in replacement: same handling of
/// `&`, `=`, `?`, `#` and the rest, and identical output for every input that
/// contains no space.
String encodeQueryValue(String value) =>
    Uri.encodeQueryComponent(value).replaceAll('+', '%20');

/// Builds `a=1&b=2` from [params], encoding both keys and values.
///
/// Empty values are **kept**, not skipped: a parameter the caller deliberately
/// set to `''` is part of the request, and silently dropping it is how a filter
/// ends up looking applied while not being. Use a `Map` that omits the key
/// entirely when a filter is unset.
String buildQueryString(Map<String, String> params) => params.entries
    .map((e) => '${encodeQueryValue(e.key)}=${encodeQueryValue(e.value)}')
    .join('&');
