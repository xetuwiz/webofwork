import 'package:webofwork/helper/local_keys.g.dart';

List<String> experienceLevels = [
  "senior",
  "midLevel",
  "junior",
];
List<String> jobType = [
  "senior",
  "midLevel",
  "junior",
];

/// Find Projects delivery filters. These mirror the web client's `[3, 7, 14, 30]`
/// day buckets; each maps to `delivery_days = ExpectedDays <= n` on the backend.
/// The old list ("less than a month", "More than 3 month", …) mostly stripped to
/// nothing in `ProjectListService.buildProjectQuery` or parsed to the *wrong* number
/// ("less than 2 month" -> 2, duplicating "2 Days"), so 5 of its 8 options
/// either no-op'd or filtered incorrectly. "More than 3 month" cannot be
/// expressed at all (`ExpectedDays <= n` has no lower bound), so it is dropped.
List<String> jobLengths = [
  "Less than 3 days",
  "Less than 7 days",
  "Less than 14 days",
  "Less than 30 days",
];

List<String> supportedFileTypesInC = [
  "png",
  "jpg",
  "jpeg",
  "pdf",
  "gif",
];

List<String> offerStatus = [LocalKeys.pending, LocalKeys.accepted, LocalKeys.declined];

enum Status {
  LOADING,
  NOT_INITIATED,
  NOT_AVAILABLE,
  INVALID,
  AVAILABLE,
}

List<String> supportedWorkFiles = [
  "png",
  "jpg",
  "jpeg",
  "pdf",
  "gif",
  // "zip",
];
