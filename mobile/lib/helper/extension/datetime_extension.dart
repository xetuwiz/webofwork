import 'package:intl/intl.dart';

import '../local_keys.g.dart';

/// One home for date rendering.
///
/// Three copies of this used to exist and drift apart:
///   * `views/home_view/components/job_card.dart` had a private
///     `formatDateTime` producing "5 minutes ago".
///   * `views/notifications_list_view/components/notification_list_tile.dart`
///     declared a **top-level** `formatDateTime` producing "Today at 3:00 PM",
///     and `job_details_proposal_card.dart` imported that *widget* file just to
///     borrow the function.
///   * `ticket_tile.dart` used a raw `DateFormat`.
///
/// Keeping one implementation in `helper/extension` breaks the widget-to-widget
/// import and stops the two wordings from diverging again.
///
/// `DateFormat` is called **without** an explicit locale on purpose. `intl` only
/// ships `en_US` date symbols unless `initializeDateFormatting` is called, and
/// nothing in this app calls it. Passing `dProvider.languageSlug` ("en_GB")
/// therefore made `intl` fall back silently via
/// `Intl.verifiedLocale(..., onFailure: (_) => null)`, which works by accident
/// and breaks the moment the locale changes. Omitting it uses the app default
/// and is the documented behaviour.
extension DateTimeFormatExtension on DateTime {
  /// Absolute, human phrasing used for list timestamps.
  ///
  /// * today      -> "Today at 3:04 PM"
  /// * this week  -> "Monday at 3:04 PM"
  /// * older      -> "March 4, 2025"
  String get friendlyDateTime {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final that = DateTime(year, month, day);
    final dayGap = today.difference(that).inDays;

    if (dayGap == 0) {
      return '${LocalKeys.todayAt} ${DateFormat("hh:mm a").format(this)}';
    }
    if (dayGap > 0 && dayGap < 7) {
      return '${DateFormat("EEEE").format(this)} '
          '${LocalKeys.at} ${DateFormat("hh:mm a").format(this)}';
    }
    return DateFormat("MMMM d, yyyy").format(this);
  }

  /// Relative phrasing used next to messages and job cards.
  ///
  /// "moments ago" / "5 minutes ago" / "2 hours ago" / "3 days ago" /
  /// "6 months ago" / "1 year ago".
  ///
  /// The unit words are built here rather than through `LocalKeys` on purpose:
  /// the phrasing is interpolated, and a single-locale app with
  /// `getString` falling back to the literal gains nothing from six more keys.
  String get relativeTime {
    final difference = DateTime.now().difference(this);
    final negative = difference.isNegative;
    final d = negative ? -difference : difference;

    String unit(int n, String singular, String plural) =>
        '$n ${n == 1 ? singular : plural} ago';

    if (d.inSeconds < 60) return 'moments ago';
    if (d.inMinutes < 60) return unit(d.inMinutes, 'minute', 'minutes');
    if (d.inHours < 24) return unit(d.inHours, 'hour', 'hours');
    if (d.inDays < 30) return unit(d.inDays, 'day', 'days');
    if (d.inDays < 365) return unit(d.inDays ~/ 30, 'month', 'months');
    return unit(d.inDays ~/ 365, 'year', 'years');
  }

  /// `dd MMM yyyy` -- the shape support tickets and order deadlines use.
  String get shortDate => DateFormat("dd MMM yyyy").format(this);
}
