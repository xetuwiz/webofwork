// Pins the query string ProjectListService sends to
// GET /api/projects/freelancer/project-list.
//
// This is a pure function of the service's public filter fields, so it is the
// one part of the request that can be unit-tested without HTTP. The route
// itself is already pinned by the route audit; what matters here is that every
// value survives model binding -- `min_price` and `max_price` bind to
// `decimal?` and `category` to `int?` in `ProjectsController.FreelancerList`,
// so a single unparseable character 400s the entire list and Find Projects silently
// renders nothing.
import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/app_static_values.dart';
import 'package:webofwork/models/category_model.dart';
import 'package:webofwork/services/project_list_service.dart';

/// Query params, decoded back out of the built query string.
Map<String, String> paramsOf(String query) => {
      for (final pair in query.split('&'))
        Uri.decodeQueryComponent(pair.split('=').first): Uri.decodeQueryComponent(
            pair.split('=').length > 1 ? pair.split('=').sublist(1).join('=') : ''),
    };

Category category(int id, {String name = 'Development'}) => Category(id: id, name: name);

void main() {
  group('paging parameters', () {
    test('always sends page and page_size', () {
      final query = paramsOf(ProjectListService().buildProjectQuery(page: 1));
      expect(query['page'], '1');
      expect(query['page_size'], '20');
    });

    test('honours an explicit page number', () {
      expect(paramsOf(ProjectListService().buildProjectQuery(page: 7))['page'], '7');
    });

    test('omits every filter when none are set', () {
      final query = paramsOf(ProjectListService().buildProjectQuery(page: 1));
      // The template also sent subCategory, country and level. This API binds
      // none of them, and an unknown query key is silently dropped by ASP.NET --
      // which is exactly how the filters appeared to work while doing nothing.
      expect(query.keys, unorderedEquals(['page', 'page_size']));
    });
  });

  group('search', () {
    test('is sent verbatim when set', () {
      final service = ProjectListService()..searchText = 'react';
      expect(paramsOf(service.buildProjectQuery(page: 1))['search'], 'react');
    });

    test('is url-encoded as %20, which is what ASP.NET Core decodes', () {
      // NOT "+": `QueryHelpers.ParseQuery` uses `Uri.UnescapeDataString`, which
      // leaves a literal `+` alone. A multi-word search sent as `web+design`
      // matched nothing, because the server searched for that exact string.
      final service = ProjectListService()..searchText = 'web design';
      final query = service.buildProjectQuery(page: 1);
      expect(query, contains('search=web%20design'));
      expect(paramsOf(query)['search'], 'web design');
    });

    test('is dropped when blank rather than sent as an empty match-all', () {
      final service = ProjectListService()..searchText = '   ';
      expect(paramsOf(service.buildProjectQuery(page: 1)), isNot(contains('search')));
    });
  });

  group('category', () {
    // `category` binds to `int?`, so a category *name* is a 400:
    // "The value 'X' is not valid." Sending the id is what works.
    test('sends the id, not the name', () {
      final service = ProjectListService()..category = category(5, name: 'Web Dev');
      final query = paramsOf(service.buildProjectQuery(page: 1));
      expect(query['category'], '5');
      expect(query['category'], isNot('Web Dev'));
    });
  });

  group('price filters', () {
    // `min_price` / `max_price` bind to `decimal?`. An unbindable value is a
    // 400 for the *whole request*, so these cover what a keyboard or a paste
    // can actually produce.
    test('a plain number is sent as a decimal literal', () {
      final service = ProjectListService()
        ..minPrice = '500'
        ..maxPrice = '1500';
      final query = paramsOf(service.buildProjectQuery(page: 1));
      expect(query['min_price'], '500.0');
      expect(query['max_price'], '1500.0');
    });

    test('a comma thousands separator is normalised, not forwarded', () {
      // .NET cannot bind "1,500" to a decimal, so the whole list request 400s.
      final service = ProjectListService()..minPrice = '1,500';
      final raw = service.buildProjectQuery(page: 1);
      expect(raw, isNot(contains(',')));
      expect(paramsOf(raw)['min_price'], '1500.0');
    });

    test('a pasted currency string is stripped down to the digits', () {
      final service = ProjectListService()
        ..minPrice = r'$500'
        ..maxPrice = ' 1500 USD ';
      final query = paramsOf(service.buildProjectQuery(page: 1));
      expect(query['min_price'], '500.0');
      expect(query['max_price'], '1500.0');
    });

    test('a bare decimal point is treated as no filter', () {
      // Sending `min_price=.` would fail to bind and 400 the request, so an
      // incomplete entry must drop the filter instead of breaking the list.
      final service = ProjectListService()..minPrice = '.';
      expect(paramsOf(service.buildProjectQuery(page: 1)), isNot(contains('min_price')));
    });

    test('an all-punctuation entry is treated as no filter', () {
      final service = ProjectListService()..maxPrice = '  -  ';
      expect(paramsOf(service.buildProjectQuery(page: 1)), isNot(contains('max_price')));
    });

    test('a negative price is dropped rather than reinterpreted as positive', () {
      // Stripping the "-" would turn "-100" into 100 -- a *different* filter
      // from what was typed, silently applied. Dropping it leaves the filter
      // unapplied, which the drawer still shows as typed.
      final service = ProjectListService()..minPrice = '-100';
      expect(paramsOf(service.buildProjectQuery(page: 1)), isNot(contains('min_price')));
    });

    test('a lone minus is treated as no filter', () {
      final service = ProjectListService()..minPrice = '-';
      expect(paramsOf(service.buildProjectQuery(page: 1)), isNot(contains('min_price')));
    });

    test('a zero lower bound is still sent', () {
      final service = ProjectListService()..minPrice = '0';
      expect(paramsOf(service.buildProjectQuery(page: 1))['min_price'], '0.0');
    });

    test('a decimal fraction survives', () {
      final service = ProjectListService()..minPrice = '99.95';
      expect(paramsOf(service.buildProjectQuery(page: 1))['min_price'], '99.95');
    });
  });

  // The rules are a function of one string, so they are pinned directly rather
  // than only through the query they end up in.
  group('parseBudgetAmount', () {
    test('plain integers', () {
      expect(parseBudgetAmount('0'), 0.0);
      expect(parseBudgetAmount('500'), 500.0);
      expect(parseBudgetAmount('1500'), 1500.0);
      expect(parseBudgetAmount('1234567'), 1234567.0);
    });

    test('a dot is always a decimal point', () {
      // The server binds with the invariant culture, and an English-locale user
      // typing 99.95 means ninety-nine ninety-five. Reading "1.500" as fifteen
      // hundred would silently apply the wrong filter.
      expect(parseBudgetAmount('99.95'), 99.95);
      expect(parseBudgetAmount('1.5'), 1.5);
      expect(parseBudgetAmount('.5'), 0.5);
    });

    test('a comma with three trailing digits is a thousands separator', () {
      expect(parseBudgetAmount('1,500'), 1500.0);
      expect(parseBudgetAmount('2,000'), 2000.0);
      expect(parseBudgetAmount('1,234,567'), 1234567.0);
    });

    test('a comma with fewer than three trailing digits is a decimal point', () {
      // The European decimal convention, which a locale keyboard can emit.
      expect(parseBudgetAmount('99,95'), 99.95);
      expect(parseBudgetAmount('1,5'), 1.5);
    });

    test('currency symbols and units are stripped', () {
      expect(parseBudgetAmount(r'$500'), 500.0);
      expect(parseBudgetAmount(r'£1,500'), 1500.0);
      expect(parseBudgetAmount('500 USD'), 500.0);
      expect(parseBudgetAmount('  1500  '), 1500.0);
      expect(parseBudgetAmount(r'US$ 1,500.50'), 1500.50);
    });

    test('a leading minus rejects the value', () {
      expect(parseBudgetAmount('-100'), isNull);
      expect(parseBudgetAmount('-'), isNull);
      expect(parseBudgetAmount('- 100'), isNull);
    });

    test('unusable input is null, not a guess', () {
      expect(parseBudgetAmount(null), isNull);
      expect(parseBudgetAmount(''), isNull);
      expect(parseBudgetAmount('   '), isNull);
      expect(parseBudgetAmount('.'), isNull);
      expect(parseBudgetAmount(','), isNull);
      expect(parseBudgetAmount('abc'), isNull);
      expect(parseBudgetAmount(r'$'), isNull);
    });

    test('a malformed number with two separators is rejected', () {
      // "1.2.3" -- guessing which separator was meant is not worth a wrong
      // filter, and forwarding it would 400 the request.
      expect(parseBudgetAmount('1.2.3'), isNull);
      expect(parseBudgetAmount('1,5,0'), isNull);
    });

    test('takes the first number when trailing text follows', () {
      expect(parseBudgetAmount('500-1000'), 500.0);
      expect(parseBudgetAmount('500 or more'), 500.0);
    });

    test('a value that cannot be a price is never negative', () {
      // A negative lower bound is meaningless for a job budget, and binding one
      // would filter out everything.
      for (final input in ['0', '1', '1,500', '.5', '1.2.3', '-1']) {
        final parsed = parseBudgetAmount(input);
        expect(parsed == null || parsed >= 0, isTrue, reason: input);
      }
    });

    test('every parse result is a string .NET can bind to decimal?', () {
      // The whole point of the function: nothing it returns may 400 the request.
      for (final input in [
        '0',
        '1',
        '99.95',
        '1,500',
        '99,95',
        r'$500',
        '500 USD',
        '1,234,567',
        r'US$ 1,500.50',
        '1.500',
        'abc',
        '',
        '.',
        '-',
        '-1',
        '1.2.3',
        '1,5,0',
        '1e5',
        '0x10',
        '  42  ',
        '1 500',
      ]) {
        final parsed = parseBudgetAmount(input);
        if (parsed == null) continue;
        // Invariant-culture decimal parse, which is what ASP.NET does here.
        expect(
          () => double.parse(parsed.toString()),
          returnsNormally,
          reason: 'unbindable result for input "$input": $parsed',
        );
        expect(parsed.isFinite, isTrue, reason: input);
        expect(parsed >= 0, isTrue, reason: input);
      }
    });
  });

  group('delivery days', () {
    // `delivery_days` binds to `int?`. The drawer supplies one of the four
    // React-parity labels, so the bucket comes from `deliveryDaysByLabel` rather
    // than stripping digits out of an arbitrary string.
    test('maps the drawer label to its delivery_days bucket', () {
      final service = ProjectListService()..length = 'Less than 30 days';
      expect(paramsOf(service.buildProjectQuery(page: 1))['delivery_days'], '30');
    });

    test('is dropped when the label is not one of the drawer options', () {
      final service = ProjectListService()..length = 'any time';
      expect(
        paramsOf(service.buildProjectQuery(page: 1)),
        isNot(contains('delivery_days')),
      );
    });

    // The drawer binds `jobLengths` labels and the service maps label -> bucket.
    // If the two drift, every length chip renders but filters nothing -- exactly
    // the silent no-op failure the React skill filter had.
    test('deliveryDaysByLabel covers every drawer length label', () {
      expect(
        ProjectListService.deliveryDaysByLabel.keys.toList(),
        unorderedEquals(jobLengths),
      );
      expect(
        ProjectListService.deliveryDaysByLabel.values.toList(),
        unorderedEquals([3, 7, 14, 30]),
      );
    });
  });

  group('all filters together', () {
    test('combine into one valid query', () {
      final service = ProjectListService()
        ..searchText = 'react'
        ..category = category(3)
        ..minPrice = r'$100'
        ..maxPrice = '2,000'
        ..length = 'Less than 14 days'
        ..skill = 'React JS';

      final query = paramsOf(service.buildProjectQuery(page: 2));

      expect(query, {
        'page': '2',
        'page_size': '20',
        'search': 'react',
        'category': '3',
        'skill': 'React JS',
        'min_price': '100.0',
        'max_price': '2000.0',
        'delivery_days': '14',
      });
    });
  });

  group('setFilters', () {
    // `setFilters` refetches whenever anything changed, and the drawer calls it
    // on every "Apply". An identical call must return before reaching
    // `fetchProjectList`.
    //
    // The assertion is `returnsNormally` rather than a mock check, and that is
    // deliberate: there is no injectable HTTP client here, so a *regression*
    // shows up by actually attempting the fetch -- which calls `getToken`,
    // reading the `late` global `sPref`, and throws `LateInitializationError`.
    // In other words this test cannot pass by accident while a fetch is
    // happening; it fails loudly if the guard is ever removed.
    test('an identical filter set does not reach the network', () {
      final service = ProjectListService();
      service.category = category(3);
      service.minPrice = '100';
      service.skill = 'React JS';

      expect(
        () => service.setFilters(
          length: null,
          maxPrice: null,
          minPrice: '100',
          category: category(3),
          skill: 'React JS',
        ),
        returnsNormally,
      );
    });

    // The skill is a string, so the no-op guard for it relies on exact string
    // equality -- an id-vs-name drift (the React bug) is precisely what this
    // filter must not reintroduce by normalising values on the way in.
    test('the no-op guard also covers a matched skill', () {
      final service = ProjectListService()..skill = 'React JS';
      expect(
        () => service.setFilters(
          length: null,
          maxPrice: null,
          minPrice: null,
          category: null,
          skill: 'React JS',
        ),
        returnsNormally,
      );
    });

    // `Category` has value equality precisely so this comparison can match.
    // Without it the guard never fires and every drawer open refetches.
    test('the no-op guard relies on Category value equality', () {
      expect(category(3, name: 'Development'), category(3, name: 'Development'));
      expect(category(3), isNot(category(4)));
    });
  });
}
