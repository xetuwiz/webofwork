import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';

import '../data/network/network_api_services.dart';
import '../helper/app_urls.dart';
import '../helper/constant_helper.dart';

/// `POST /api/support/tickets` with `CreateSupportTicketRequest`, which has
/// exactly two fields:
///
/// ```csharp
/// public record CreateSupportTicketRequest {
///     public string Subject { get; init; }
///     public string Message { get; init; }
/// }
/// ```
///
/// The previous body sent `title`, `subject`, `priority`, `description` and
/// `department`. Four of those five are silently discarded by the model binder
/// (snake_case `PropertyNamingPolicy` matches only `subject`), and
/// `createTicket` was declared as `required Department selectedDepartment`, so
/// the call could not even be made without a department that the API does not
/// offer a way to fetch.
class SupportTicketCreateService with ChangeNotifier {
  Future<bool> createTicket({required String subject, required String message}) async {
    final responseData = await NetworkApiServices().postApi({
      'subject': subject,
      'message': message
    }, AppUrls.createTicketUrl, LocalKeys.createTicket, headers: acceptJsonAuthHeader);

    if (responseData is Map) {
      LocalKeys.ticketCreatedSuccessfully.showToast();
      return true;
    }
    return false;
  }
}
