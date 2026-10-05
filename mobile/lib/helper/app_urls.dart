import '/customizations.dart';
import 'query_string.dart';

/// All API endpoints for the WebOfWork freelancer app.
/// Base URL is configured in customizations.dart via --dart-define=API_BASE=...
class AppUrls {
  // ── Auth ────────────────────────────────────────────────────────────────────
  // Registration on this API is a three-step OTP flow, not a single POST
  // (see `AuthController`). The previous `/api/auth/register/` never existed:
  // every sign-up 404'd, and the app then read a `token` and an
  // `email_verify_token` off a response that could not have arrived.
  static String signInUrl = '$baseEndPoint/api/auth/login/';
  static String signOutUrl = '$baseEndPoint/api/auth/logout/';
  static String tokenRefreshUrl = '$baseEndPoint/api/auth/refresh/';

  /// Step 1 -- caches the registration in Redis for 10 minutes and emails a code.
  /// Body `{full_name, email, password}`. There is no `username`, no `phone`,
  /// and no separate first/last name: the server takes one `full_name` string
  /// and rejects anything longer than 40 characters.
  static String registerSendOtpUrl = '$baseEndPoint/api/auth/send-otp/';

  /// Step 2 -- body `{email, otp_code}` -> `{message, user, access, access_token,
  /// refresh}`. This is the step that actually creates the account and returns
  /// the session, so the app persists `access` + `refresh` from here.
  static String registerVerifyOtpUrl = '$baseEndPoint/api/auth/verify-otp/';

  /// Re-sends the registration code. Doubles as the liveness probe: it 400s with
  /// `non_field_errors` once the 10-minute Redis record has expired.
  static String resendRegistrationOtpUrl = '$baseEndPoint/api/auth/resend-otp/';

  /// Password reset is a **magic link**, not an OTP. This posts `{email}` and
  /// always answers 200 ("if the email exists") so it cannot be used to probe
  /// for accounts. The link opens the web client with `?uid=&token=`, which is
  /// why the app has no in-app "enter the code" step: the mobile client cannot
  /// receive a URL the user clicks in a browser.
  static String forgotPasswordUrl = '$baseEndPoint/api/auth/forgot-password/';

  /// Used by the web client, not the app. Kept for reference and for the
  /// `POST /api/auth/reset-password` body shape (`{uid, token, new_password,
  /// confirm_password}`).
  static String resetPasswordUrl = '$baseEndPoint/api/auth/reset-password/';

  /// Current user identity: id, full_name, email, profile_photo. This is the
  /// only place a user's name/email/photo is exposed -- the freelancer profile
  /// endpoint carries none of them.
  static String authMeUrl = '$baseEndPoint/api/auth/me/';

  // ── Profile ─────────────────────────────────────────────────────────────────
  /// Freelancer's own profile: tagline, bio, phone, experience_level, rating.
  /// NOTE: read-only. The write route is [freelancerProfileUpdateUrl].
  static String profileInfoUrl = '$baseEndPoint/api/profile/freelancer/me/';
  static String fullProfileInfoUrl = '$baseEndPoint/api/profile/freelancer/profile/';

  /// Write endpoint for the freelancer profile.
  ///
  /// PATCH + JSON, and no trailing slash, because the route is declared as an
  /// absolute path on FreelancersController
  /// (`[HttpPatch("/api/profile/freelancer/me")]`). The profile endpoint above
  /// only has a GET route, so the old client's POST-multipart here returned 405
  /// and saved nothing.
  static String freelancerProfileUpdateUrl = '$baseEndPoint/api/profile/freelancer/me';

  /// PayPal / payment settings.
  ///
  /// `GET` and `PUT` on the same path. The body is the object
  /// `{"paypal_email": "..."}`: the action takes
  /// `[FromBody] PaymentSettingsRequest`, a record with one named field, so an
  /// earlier `[FromBody] string paypalEmail` could not bind the object the web
  /// client sends and every save silently stored an empty address. The route is
  /// `freelancer/payment-settings`, not `payment-settings`; the old value 404'd.
  static String paymentSettingsUrl =
      '$baseEndPoint/api/profile/freelancer/payment-settings';

  // ── Push notifications ───────────────────────────────────────────────────────

  /// FCM device registration. `POST` registers, `GET` lists the user's devices,
  /// and `DELETE` takes a `?token=` query parameter.
  ///
  /// Upsert is keyed on the token because FCM rotates it on reinstall and on
  /// restore, and the client re-registers on every cold start -- so this is hit
  /// routinely with the same value.
  static String registerDeviceUrl = '$baseEndPoint/api/notifications/devices';

  /// Channel switches and per-type mutes.
  static String notificationPreferencesUrl =
      '$baseEndPoint/api/notifications/preferences';

  /// The server's full notification-type vocabulary, so the settings screen never
  /// hardcodes the list.
  static String notificationPreferenceTypesUrl =
      '$baseEndPoint/api/notifications/preferences/types';

  // ── Dashboard & Todos ───────────────────────────────────────────────────────
  static String todosUrl = '$baseEndPoint/api/profile/todos';
  static String recommendedProjectsUrl =
      '$baseEndPoint/api/profile/freelancer/recommended-projects';

  // ── Invitations ─────────────────────────────────────────────────────────────
  /// Received invitations (GET). Paged envelope: {results, count, page,
  /// page_size, total_pages, next, previous}. Filter with ?status=.
  static String receivedInvitationsUrl =
      '$baseEndPoint/api/projects/invitations/received';

  /// Accept / decline one invitation:
  /// PATCH /api/projects/invitations/{id}/update-status  body {"status": "..."}
  /// Answers 204 No Content.
  static String invitationStatusUrl(int id) =>
      '$baseEndPoint/api/projects/invitations/$id/update-status';

  // ── Projects (Find Projects) ────────────────────────────────────────────────────
  /// List of open projects freelancer can bid on.
  static String projectListUrl = '$baseEndPoint/api/projects/freelancer/project-list/';

  /// Single project details: append "/{id}/"
  static String projectDetailsUrl = '$baseEndPoint/api/projects/';

  /// Freelancer-shaped project detail, which is the one that carries
  /// `client_info`, `expected_deliverables`, `requirements`, `proposals_count`
  /// and `is_applied`: `GET /api/projects/{id}/details`.
  ///
  /// Note the `/details` suffix — this is **not** the same endpoint as
  /// [projectDetailsUrl] + `/{id}`. `ProjectsController.Get` is `[AllowAnonymous]`
  /// and returns `MapToResponse(...)`, which leaves `client_info` null and
  /// carries no `is_applied`, so a client reading it cannot tell whether it has
  /// already applied. `ProjectsController.Details` is
  /// `[Authorize(Policy = "RequireFreelancer")]` and populates all of them.
  ///
  /// Prefer the builder below over concatenating by hand: it is the only place
  /// that knows the `/details` suffix is required.
  static String projectFullDetailsUrl = '$baseEndPoint/api/projects/';

  /// `GET /api/projects/{id}/details` for [projectId].
  static String projectDetailsForFreelancerUrl(Object projectId) =>
      '$baseEndPoint/api/projects/$projectId/details';

  // ── Proposals ───────────────────────────────────────────────────────────────
  /// Freelancer's submitted proposals (GET).
  ///
  /// Envelope: `{count, next, previous, results}`.
  ///
  /// This used to point at `GET /api/proposals/`, which **is not a route**.
  /// `ProposalsController` is `[Route("api/[controller]")]` and its only GETs are
  /// `my` and `project/{projectId}` -- there is no handler on the bare prefix --
  /// so the request 404'd and the My Proposals list could never load. The
  /// absolute alias `~/api/projects/my-proposals` is what the web client calls
  /// (`features/freelancer/proposals/components/proposalApi.js`) and is used
  /// here for the same reason.
  static String myProposalsUrl = '$baseEndPoint/api/projects/my-proposals';

  /// Builds the request for a page of proposals.
  ///
  /// Pass [next] (the relative `?page=N&pageSize=M` the server returned) to
  /// follow its own pagination, or [page] to start from a specific page, and
  /// optionally a [status] filter. The server parses `status` with
  /// `Enum.TryParse<ProposalStatus>`, so an unrecognised value is ignored rather
  /// than rejected.
  ///
  /// The bare relative `next` string is **not** a URL, so it has to be joined
  /// here -- passing it straight to the network layer was the other half of the
  /// broken pagination.
  static String myProposalsUrlWithQuery({
    int? page,
    int pageSize = 10,
    String? status,
    String? next,
  }) {
    if (next != null && next.isNotEmpty) {
      // The server's `next` is relative to the **request path**, not the host
      // root, so it has to be joined onto the endpoint. Joining it onto
      // `baseEndPoint` instead produced `http://host?page=2` -- the proposals
      // path was silently dropped, which is a 404 on a route that exists.
      return next.startsWith('http') ? next : '$myProposalsUrl$next';
    }
    final params = <String, String>{
      'page': '${page ?? 1}',
      'pageSize': '$pageSize',
    };
    if (status != null && status.isNotEmpty) params['status'] = status;
    return '$myProposalsUrl?${buildQueryString(params)}';
  }

  /// Submit a new proposal (POST).
  ///
  /// `POST /api/proposals` and `POST /api/projects/create-proposal` bind the same
  /// `CreateProposalRequest`. The latter is the route the web client calls, and
  /// both now share one validation and one error mapping on the server.
  ///
  /// Body: `{ "project": <int>, "package": "<uuid>", "cover_letter": "..." }`
  ///
  /// There is deliberately **no** amount or delivery-days field. The request DTO
  /// does carry `bid_amount` and `delivery_days`, but
  /// `ProposalService.CreateProposalAsync` overwrites both with the chosen
  /// package's own `Price` / `DeliveryDays` and never reads them.
  static String submitProposalUrl = '$baseEndPoint/api/projects/create-proposal';

  /// Withdraw a submitted proposal: `DELETE /api/proposals/{id}`.
  ///
  /// Answers **204 No Content**, and 400s with
  /// `{ "error": "Only pending proposals can be withdrawn." }` for anything that
  /// is not `Pending` — so the client hides the action rather than showing a
  /// button that is guaranteed to be rejected.
  ///
  /// The absolute alias `~/api/projects/proposals/{id}` is the same handler.
  static String proposalDetailUrl = '$baseEndPoint/api/proposals/';

  // ── Invitations ─────────────────────────────────────────────────────────────
  /// List invitations (GET) + accept/decline (POST to /{id}/accept/ or /{id}/decline/).
  static String invitationsUrl = '$baseEndPoint/api/invitations/';

  // ── Contracts ───────────────────────────────────────────────────────────────
  /// Freelancer's contracts (GET). Envelope: {count, next, previous, results}.
  /// Query: ?status=active&search=ORD-1&role=freelancer
  static String contractsUrl = '$baseEndPoint/api/contracts/';

  /// Single contract: append "/{id}"
  static String contractDetailUrl = '$baseEndPoint/api/contracts/';

  /// Review & sign an offer: POST /api/contracts/{id}/accept
  ///
  /// The old value pointed at `/{id}/sign/`, which is not a route on this API
  /// (`ContractsController` registers `{id}/accept`), so it 404'd. Body is
  /// {"signature": <base64 png data url or https url>, "signature_type": "type"
  /// | "draw"}; answers 204 No Content.
  static String contractAcceptUrl = '$baseEndPoint/api/contracts/';

  // ── Deliverables ─────────────────────────────────────────────────────────────
  /// Submit a deliverable: POST /api/deliverables/contract/{contractId}
  /// multipart with `title`, `notes`, `deliverable_type`, and either
  /// `link_url` or a file part named `file`. Server cap is 20 MB.
  static String deliverablesUrl = '$baseEndPoint/api/deliverables/';

  /// List a contract's deliverables: GET /api/deliverables/contract/{contractId}
  static String contractDeliverablesUrl = '$baseEndPoint/api/deliverables/contract/';

  // ── Revisions ────────────────────────────────────────────────────────────────
  /// GET /api/revisions/contract/{contractId}
  static String contractRevisionsUrl = '$baseEndPoint/api/revisions/contract/';

  /// Accept or decline: POST /api/revisions/{id}/action
  /// body {"accept": true|false, "rejection_message": "..."}.
  static String revisionActionUrl(int id) => '$baseEndPoint/api/revisions/$id/action';

  // ── Disputes ─────────────────────────────────────────────────────────────────
  /// Raise a dispute: POST /api/contracts/dispute
  ///
  /// The body carries the contract id. Preferred over
  /// `POST /api/disputes/contract/{id}`, whose service re-derives the id from the
  /// body and 404s when it is absent.
  static String disputesUrl = '$baseEndPoint/api/contracts/dispute';

  /// Read a contract's dispute: GET /api/disputes/contract/{contractId}
  static String contractDisputeUrl = '$baseEndPoint/api/disputes/contract/';

  // ── Reviews ──────────────────────────────────────────────────────────────────
  /// Submit a review: POST /api/reviews
  /// body {"contract": <id>, "reviewee": <guid>, "rating": 1-5, "comment": "...",
  /// "review_type": "client"}. Answers 201.
  static String reviewsUrl = '$baseEndPoint/api/reviews/';

  // ── Notifications ─────────────────────────────────────────────────────────────
  /// List notifications (GET). Envelope: {data: {unread_count, total_count, items}}.
  /// The unread count is ONLY available here -- there is no unread-count route.
  static String notificationsUrl = '$baseEndPoint/api/notifications/';

  /// Mark every notification read:
  /// PATCH /api/notifications/read-all
  ///
  /// The old value was a POST to `/api/notifications/mark-all-read/`, which is
  /// neither the right verb nor a real path, so it 404'd.
  static String markAllReadUrl = '$baseEndPoint/api/notifications/read-all';

  /// Mark one notification read: PATCH /api/notifications/{id}/read
  static String notificationReadUrl(int id) => '$baseEndPoint/api/notifications/$id/read';

  // ── Todos ─────────────────────────────────────────────────────────────────────
  /// Personal task list (GET / POST).

  // ── Earnings ──────────────────────────────────────────────────────────────────
  /// Earnings overview: GET /api/payments/freelancer/earnings
  ///
  /// The old value pointed at `/api/payouts/earnings/`, which is not a route on
  /// this API. Query params are `status`, `range` (1m|3m|6m|1y), `page`,
  /// `pageSize` -- note `range`, not `period`. Envelope:
  /// {count, next, previous, results: {total_earning, total_projects, contracts}}.
  static String earningsUrl = '$baseEndPoint/api/payments/freelancer/earnings';

  /// Transaction history (same filters): GET /api/payments/freelancer/transaction-history
  static String earningsHistoryUrl =
      '$baseEndPoint/api/payments/freelancer/transaction-history';

  // ── Chat ──────────────────────────────────────────────────────────────────────
  /// Chat room list: GET /api/chat/rooms
  static String chatListUrl = '$baseEndPoint/api/chat/rooms/';

  /// Messages in a room: append "/{id}/messages"
  static String chatMessagesUrl = '$baseEndPoint/api/chat/rooms/';

  /// Send a message: POST /api/chat/rooms/{id}/messages
  static String messageSendUrl = '$baseEndPoint/api/chat/rooms/';

  /// Get or create the room for a contract: POST /api/chat/rooms
  ///
  /// Body is {"contract_id": <id>}. The old value added a `get-or-create`
  /// segment, which is not a route -- `ChatController` registers a plain
  /// `POST rooms` -- so the "Chat with client" button 404'd and did nothing.
  static String chatGetOrCreateUrl = '$baseEndPoint/api/chat/rooms';

  /// Mark a room read: PATCH /api/chat/rooms/{id}/read
  static String chatRoomReadUrl(int id) => '$baseEndPoint/api/chat/rooms/$id/read';

  // ── Support ───────────────────────────────────────────────────────────────────
  /// Ticket list (GET, paged envelope {items, total_count, open_count, page,
  /// page_size, total_pages}) + create (POST {subject, message}).
  ///
  /// There is NO departments route on this API, so the old "pick a department"
  /// step has to go: it always fetched an empty list and blocked submission.
  static String ticketListUrl = '$baseEndPoint/api/support/tickets/';

  /// Create a new ticket (POST). Body is exactly {subject, message} --
  /// `CreateSupportTicketRequest` has no title, priority or department.
  static String createTicketUrl = '$baseEndPoint/api/support/tickets/';

  /// Cancel your own ticket: PATCH /api/support/tickets/{id}/cancel
  ///
  /// Answers the updated ticket. Only `open` tickets can be cancelled; the API
  /// returns 400 ("Only open support tickets can be cancelled.") otherwise.
  ///
  /// There is no per-ticket message thread on this API, so the old
  /// `ticketChatUrl` / `fetchTicketChatUrl` / `sendTicketMessageUrl` constants
  /// were removed along with the `ticket_chat_view` screen.
  static String cancelTicketUrl(int id) => '$baseEndPoint/api/support/tickets/$id/cancel';

  // ── Content (public) ───────────────────────────────────────────────────────────
  /// All three are [AllowAnonymous] and share one envelope:
  /// {count, next, previous, results: [{id, category, heading, description}]}.
  static String faqUrl = '$baseEndPoint/api/common/public/faq';
  static String termsUrl = '$baseEndPoint/api/common/public/terms-and-conditions';
  static String privacyUrl = '$baseEndPoint/api/common/public/privacy-policy';

  /// Platform commission percentage, used by the proposal fee breakdown.
  /// Answers {"commission_percentage": "10.00"} -- a decimal *string*.
  static String platformPercentageUrl = '$baseEndPoint/api/common/platform-percentage';

  // ── Categories & Skills ────────────────────────────────────────────────────────
  /// All categories (GET).
  static String categoryUrl = '$baseEndPoint/api/categories/';

  /// Every category in one shot. `no_pagination=true` makes the endpoint return a
  /// bare JSON array instead of the `{count, results}` envelope, so a dropdown
  /// never has to walk pages.
  static String allCategoriesUrl = '$baseEndPoint/api/categories/?no_pagination=true';

  /// Every skill in one shot. The Find Projects `skill` filter matches on **name**
  /// server-side (`x.Skill.Name.ToLower() == skill.ToLower()`), so the dropdown
  /// stores the name, not the id. No bare `/api/skills/` route exists; skills
  /// live under categories via `GET /api/categories/skills`.
  static String skillsUrl = '$baseEndPoint/api/categories/skills?no_pagination=true';

  // ── Portfolio ──────────────────────────────────────────────────────────────────
  /// Add a portfolio item: POST /api/profile/freelancer/portfolio
  /// body {title, description, image_url}. Item-level PATCH and DELETE are
  /// `PATCH|DELETE /api/profile/freelancer/portfolio/{id}` (same body shape).
  static String portfolioUrl = '$baseEndPoint/api/profile/freelancer/portfolio';

  /// Uploads a portfolio image as `multipart/form-data` and returns
  /// `{ "url": "https://res.cloudinary.com/..." }`, which then goes into
  /// `image_url` on the ordinary JSON create/PATCH above.
  ///
  /// The web client instead posts to Cloudinary directly with an unsigned
  /// upload preset. A new route rather than a multipart overload of
  /// `portfolioUrl`, because that one is `[FromBody]` JSON and the existing web
  /// client would 415.
  static String portfolioImageUploadUrl =
      '$baseEndPoint/api/profile/freelancer/portfolio/upload-image';

  // ── Platform Settings ─────────────────────────────────────────────────────────
  // There is deliberately no currency/platform-settings URL. The only settings
  // routes on this API are `GET`/`PATCH /api/admin/settings/global`
  // (`PlatformSettingsController`), which are `[Authorize(Policy =
  // RequireAdmin)]` -- unreachable from a freelancer client, and not something a
  // client app should read. `DynamicsService` therefore uses its own declared
  // locale and currency defaults rather than fetching them from a 404.
  //
  // For the same reason there is no `/api/platform-settings/translations/` URL:
  // the app ships English `LocalKeys` constants and `AppStringService.getString`
  // returns them when there is no override, so there is nothing to translate.

  // ── Compatibility aliases (old name → new URL) ────────────────────────────────
  // Services still use these names from the old template. They are mapped to
  // the correct WebOfWork endpoints so we don't need to refactor every service
  // file in Phase 1. They will be cleaned up screen by screen in later phases.
  //
  // The `order*` aliases (myOrdersListUrl, orderDetailsUrl, orderAcceptUrl,
  // orderCancelUrl) have been removed: the contract screens now use the canonical
  // contractsUrl / contractDetailUrl / contractAcceptUrl directly. Two of the four
  // had no callers at all, so they were pure dead weight.
  static String submitWorkUrl = deliverablesUrl;
  static String myOffersUrl = '$baseEndPoint/api/contracts?status=offered';
  static String notificationsListUrl = notificationsUrl;

  /// PATCH /api/notifications/read-all
  static String updateNotificationUrl = markAllReadUrl;
  static String conversationUrl = '$baseEndPoint/api/chat/rooms/';
  static String stListUrl = '$baseEndPoint/api/support/tickets/';
  static String projectStatusChangeUrl = projectDetailsUrl;
  static String changePasswordUrl = '$baseEndPoint/api/auth/change-password/';
  static String deleteAccountUrl = '$baseEndPoint/api/auth/delete-account/';
  static String sendOtpUrl = resendRegistrationOtpUrl;

  /// `POST /api/profile/switch-role` body `{"role": "freelancer"}` -> 200.
  ///
  /// Required immediately after registration. `AuthService.CreateRegistrationUserAsync`
  /// hard-codes `CurrentRole = UserRole.Client` (the server's own comment: "Public
  /// signup is always a client"), because the public form belongs to the **client**
  /// web app. Every freelancer route on this API is gated on
  /// `[Authorize(Policy = "RequireFreelancer")]`, so without this call a
  /// freshly-registered account is a client and the app's own dashboard, jobs,
  /// contracts, chat and notifications all 403. This is the single marketplace
  /// onboarding step, and it is `[Authorize]`-only -- any signed-in user may
  /// switch, which is the intended path in both directions.
  static String switchRoleUrl = '$baseEndPoint/api/profile/switch-role';

  /// `POST /api/auth/verify-email` -- this verifies an **email change** made from
  /// the profile screen (`VerifyEmailChangeRequest(Otp)`). It is *not* part of
  /// registration: registration verification is `registerVerifyOtpUrl` above and
  /// takes `{email, otp_code}`. The two were previously conflated, which is why
  /// the sign-up flow read a session out of a response that never contained one.
  static String emailVerifyUrl = '$baseEndPoint/api/auth/verify-email/';
  static String projectFilter = projectListUrl;
  static String profileImagePath = '$baseEndPoint/media/';
}
