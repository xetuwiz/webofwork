import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/models/category_model.dart';
import 'package:webofwork/services/project_list_service.dart';

void main() {
  group('CategoryModel envelope', () {
    // `GET /api/categories?no_pagination=true` -> a bare array. This is the shape
    // both the web client's `categoryApi.js` and the mobile picker ask for.
    test('reads the bare array the no_pagination branch returns', () {
      final model = CategoryModel.fromJson([
        {'id': 1, 'name': 'Design'},
        {'id': 2, 'name': 'Development'},
      ]);

      expect(model.data.map((c) => c.name), ['Design', 'Development']);
      expect(model.data.map((c) => c.id), [1, 2]);
      expect(model.next, isNull);
    });

    // Without this the dropdown rendered a permanent "No results found": the model
    // read `data` / `links.next`, and this API sends neither key.
    test('reads the paged envelope PagedResponse<CategoryResponse> produces', () {
      final model = CategoryModel.fromJson({
        'results': [
          {'id': 3, 'name': 'Writing'},
          {'id': 4, 'name': 'Marketing'},
        ],
        'count': 9,
        'page': 1,
        'page_size': 2,
        'total_pages': 5,
        'next': 'http://localhost:5000/api/categories?page=2&page_size=2',
        'previous': null,
      });

      expect(model.data.length, 2);
      expect(model.data.first.name, 'Writing');
      expect(model.next, 'http://localhost:5000/api/categories?page=2&page_size=2');
    });

    test('tolerates the Laravel-ish data array the template expected', () {
      final model = CategoryModel.fromJson({
        'data': [
          {'id': 7, 'name': 'Video'},
        ],
        'links': {'next': '?page=2'},
      });

      expect(model.data.single.name, 'Video');
    });

    test('an unknown shape reports no rows instead of throwing', () {
      expect(CategoryModel.fromJson(null).data, isEmpty);
      expect(CategoryModel.fromJson('nonsense').data, isEmpty);
      expect(CategoryModel.fromJson(<String, dynamic>{}).data, isEmpty);
    });

    test('a numeric id is coerced so it binds to the job-list int parameter', () {
      // `?category=` is bound to `int? category` server-side, so a "1" string
      // would 400 with "The value '1' is not valid".
      final model = CategoryModel.fromJson([
        {'id': '12', 'name': 'Data'},
      ]);

      expect(model.data.single.id, 12);
      expect(model.data.single.id, isA<int>());
    });
  });

  group('Category value equality', () {
    test('two rows with the same id and name compare equal', () {
      final a = Category.fromJson(const {'id': 5, 'name': 'Legal'});
      final b = Category.fromJson(const {'id': 5, 'name': 'Legal'});

      expect(a, b);
      expect(a.hashCode, b.hashCode);
    });

    test('a different id does not compare equal', () {
      expect(
        Category.fromJson(const {'id': 5, 'name': 'Legal'}),
        isNot(Category.fromJson(const {'id': 6, 'name': 'Legal'})),
      );
    });
  });

  group('ProjectListService.buildProjectQuery', () {
    // Only these are bound by `ProjectsController.FreelancerList`. The template
    // also sent `subCategory`, `country` and `level`, none of which exist on the
    // route, so they were collected and dropped on the floor.
    test('sends only the parameters the endpoint binds, when nothing is set', () {
      final query = ProjectListService().buildProjectQuery(page: 1);
      final params = Uri.splitQueryString(query);

      expect(params['page'], '1');
      expect(params['page_size'], '20');
      expect(params.keys, isNot(contains('subCategory')));
      expect(params.keys, isNot(contains('country')));
      expect(params.keys, isNot(contains('level')));
    });

    test('sends the category as an id, not a name', () {
      final service = ProjectListService()..category = Category(id: 3, name: 'Design');
      final params = Uri.splitQueryString(service.buildProjectQuery(page: 1));

      expect(params['category'], '3');
    });

    test('omits blank filters rather than sending empty strings', () {
      final service = ProjectListService()
        ..minPrice = ''
        ..maxPrice = '  '
        ..length = null;
      final params = Uri.splitQueryString(service.buildProjectQuery(page: 1));

      expect(params.containsKey('min_price'), isFalse);
      expect(params.containsKey('max_price'), isFalse);
      expect(params.containsKey('delivery_days'), isFalse);
    });

    test('maps the drawer label to its delivery_days bucket', () {
      // `delivery_days` binds to `int?` and the server compares
      // `ExpectedDays <= delivery_days`, so the four React-parity drawer labels
      // map 1:1 onto day buckets -- no digit-stripping of free-form labels,
      // which silently mis-bucketed "less than 2 month" as `2`.
      final service = ProjectListService()..length = 'Less than 14 days';
      final params = Uri.splitQueryString(service.buildProjectQuery(page: 1));

      expect(params['delivery_days'], '14');
    });

    test('a length with no digits is dropped instead of sent as garbage', () {
      final service = ProjectListService()..length = 'any time';
      final params = Uri.splitQueryString(service.buildProjectQuery(page: 1));

      expect(params.containsKey('delivery_days'), isFalse);
    });

    test('page and page_size advance together', () {
      final params = Uri.splitQueryString(ProjectListService().buildProjectQuery(page: 4));

      expect(params['page'], '4');
      expect(params['page_size'], '20');
    });
  });

  group('ProjectListService paging state', () {
    // `project-list` answers `{results, count, page, page_size, total_pages}` and
    // sends **no `next` URL**. The template read `json["next"]`, so the old
    // `nextPage` was permanently null and `fetchNextPage` returned on its first
    // line: Find Projects could only ever show the first 20 projects.
    test('hasMore is driven by page and count, not by a next URL', () {
      final service = ProjectListService();

      // Fresh state: one page of 20, nothing known beyond it.
      expect(service.hasMore, isFalse);
    });
  });
}
