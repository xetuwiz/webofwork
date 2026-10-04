// Query-string encoding, pinned against how ASP.NET Core actually decodes.
//
// The trap: `Uri.encodeQueryComponent` writes a space as `+`, and
// `Uri.decodeQueryComponent` reads a `+` back as a space. Round-tripping a value
// through both therefore *passes* while the server sees something entirely
// different -- `QueryHelpers.ParseQuery` decodes with `Uri.UnescapeDataString`,
// which resolves `%XX` only and leaves `+` as a literal plus sign.
//
// So the assertions below deliberately avoid the client's own decoder and check
// the literal wire bytes instead.
import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/helper/query_string.dart';

/// Decodes a query value the way ASP.NET Core does: `%XX` only, `+` is literal.
///
/// Mirrors `QueryHelpers.ParseQuery`, which is what backs `Request.Query`.
String decodeLikeAspNet(String value) {
  final buffer = StringBuffer();
  for (var i = 0; i < value.length; i++) {
    if (value[i] == '%' && i + 2 < value.length) {
      final hex = value.substring(i + 1, i + 3);
      final byte = int.tryParse(hex, radix: 16);
      if (byte != null) {
        buffer.writeCharCode(byte);
        i += 2;
        continue;
      }
    }
    buffer.write(value[i]);
  }
  return buffer.toString();
}

void main() {
  group('encodeQueryValue', () {
    test('a space becomes %20, never +', () {
      // THE regression. Dart's own encoder produces "web+design"; ASP.NET Core
      // would then search for the literal "web+design" and match nothing.
      expect(encodeQueryValue('web design'), 'web%20design');
      expect(encodeQueryValue('web design'), isNot(contains('+')));
    });

    test('survives a multi-word search as the server will read it', () {
      final encoded = encodeQueryValue('senior react developer');
      expect(decodeLikeAspNet(encoded), 'senior react developer');
    });

    test('is a drop-in for the Dart encoder when there is no space', () {
      const values = [
        'react',
        'a&b',
        'a=b',
        'a?b',
        'a#b',
        'a/b',
        'a+b',
        '100%',
        'CSharp',
        'sales/marketing',
        'ümlaut',
        '',
      ];
      for (final value in values) {
        expect(encodeQueryValue(value), Uri.encodeQueryComponent(value),
            reason: 'must not change non-space input: "$value"');
      }
    });

    test('a literal plus stays a literal plus', () {
      // The mirror of the space case: %2B survives as a plus, so a search for
      // "c++" is not turned into "c  " by the server.
      expect(decodeLikeAspNet(encodeQueryValue('c++')), 'c++');
    });

    test('an ampersand cannot break out into a new parameter', () {
      final encoded = encodeQueryValue('a&b=c');
      expect(encoded, isNot(contains('&')));
      expect(decodeLikeAspNet(encoded), 'a&b=c');
    });

    test('a space combined with a separator is encoded safely', () {
      final encoded = encodeQueryValue('design & build');
      expect(decodeLikeAspNet(encoded), 'design & build');
      expect(encoded.split('&').length, 1);
    });
  });

  group('buildQueryString', () {
    test('joins pairs with &', () {
      expect(
        buildQueryString({'page': '1', 'page_size': '20'}),
        'page=1&page_size=20',
      );
    });

    test('encodes spaces in values as %20', () {
      expect(
        buildQueryString({'search': 'web design', 'page': '1'}),
        'search=web%20design&page=1',
      );
    });

    test('encodes spaces in keys too', () {
      // Rare, but the helper is symmetric so a key can never corrupt the parse.
      expect(decodeLikeAspNet(buildQueryString({'a b': 'c'})), 'a b=c');
    });

    test('an empty value is kept, not dropped', () {
      // Silently omitting a set-but-empty parameter is how a filter looks
      // applied in the UI while not being sent at all.
      expect(buildQueryString({'q': ''}), 'q=');
    });

    test('an empty map produces an empty string', () {
      expect(buildQueryString({}), '');
    });

    test('insertion order is preserved, so paging URLs are diffable', () {
      expect(
        buildQueryString({'page': '3', 'page_size': '20', 'search': 'go'}),
        'page=3&page_size=20&search=go',
      );
    });

    test('round-trips every pair through the server-side decoder', () {
      final params = {
        'search': 'senior react developer',
        'category': '3',
        'min_price': '99.95',
      };
      final query = buildQueryString(params);

      final decoded = <String, String>{};
      for (final pair in query.split('&')) {
        final parts = pair.split('=');
        decoded[decodeLikeAspNet(parts.first)] =
            decodeLikeAspNet(parts.sublist(1).join('='));
      }

      expect(decoded, params);
    });
  });
}
