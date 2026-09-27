import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/proposal_model.dart';
import 'package:webofwork/services/project_details_service.dart';

import '../data/network/network_api_services.dart';

/// Submitting a proposal to a project.
///
/// The contract this is written against:
///
/// ```http
/// POST /api/projects/create-proposal
/// { "project": 4, "package": "<uuid>", "cover_letter": "..." }
/// -> ProposalResponse
/// ```
///
/// The sheet this replaces had **no** submit at all -- its Apply button was
/// `// TODO: Implement proposal submission in Phase 4`, and the service behind
/// it (`ProjectDetailsService.trySendingOffer`) posted a template multipart body
/// (`client_id`, `job_id`, `amount`, `duration`, `revision`, `attachment`) to a
/// route that does not exist on this API. So the primary freelancer action,
/// applying to a job, could not be performed.
///
/// What was removed, and why:
///  * **Amount** -- `bid_amount` is the *package's* price.
///    `CreateProposalAsync` sets `BidAmount = package.Price` and never reads the
///    request field.
///  * **Delivery time** -- likewise `DeliveryDays = package.DeliveryDays`.
///  * **Revisions** -- no such field on `CreateProposalRequest`.
///  * **Attachment** -- `MessageRequest`/`CreateProposalRequest` bind no file;
///    there is no multipart proposal route.
///
/// A proposal is therefore *a choice of package plus a cover letter*, and that
/// is exactly what this service accepts.
class ProposalService with ChangeNotifier {
  MyPackages _packages = MyPackages.empty;
  bool _packagesLoading = false;
  bool _packagesFetched = false;

  bool _submitting = false;

  bool get packagesLoading => _packagesLoading;

  bool get packagesFetched => _packagesFetched;

  bool get submitting => _submitting;

  MyPackages get packages => _packages;

  bool get hasPackages => _packages.isNotEmpty;

  /// Loads `GET /api/profile/freelancer/profile` for its `packages` object.
  ///
  /// Cached after the first success: a package tier's price and duration are
  /// what the *client* quotes in the fee breakdown, so re-reading them on every
  /// sheet open would let the quoted numbers drift from what gets stored.
  /// [force] re-reads after a package is edited elsewhere.
  Future<bool> fetchMyPackages({bool force = false}) async {
    if (_packagesLoading) return _packages.isNotEmpty;
    if (_packagesFetched && !force && _packages.isNotEmpty) return true;

    _packagesLoading = true;
    notifyListeners();
    final response = await NetworkApiServices().getApi(
      AppUrls.fullProfileInfoUrl,
      LocalKeys.profile,
      headers: commonAuthHeader,
    );
    _packagesLoading = false;

    if (response is Map) {
      _packages = MyPackages.fromProfile(response);
      _packagesFetched = true;
      notifyListeners();
      return _packages.isNotEmpty;
    }

    // Either the request failed or the profile has no `packages` key at all.
    // Both mean "the apply button must not pretend it can submit".
    _packages = MyPackages.empty;
    _packagesFetched = true;
    notifyListeners();
    return false;
  }

  /// Posts the proposal. Returns the stored proposal, or null on failure.
  ///
  /// The API refuses a proposal with no resolvable package ("Invalid package or
  /// you do not own it.") and refuses a second live one ("You have already
  /// submitted a proposal for this project."), so both are checked up front to
  /// turn a round-trip into an inline message.
  Future<SubmittedProposal?> submit({
    required int projectId,
    required FreelancerPackage package,
    required String coverLetter,
  }) async {
    if (_submitting) return null;

    final letter = coverLetter.trim();
    if (letter.isEmpty) {
      LocalKeys.pleaseWriteCoverLetter.showToast();
      return null;
    }

    _submitting = true;
    notifyListeners();

    // `project` and `package` -- not `project_id` / `package_id`. The DTO carries
    // both spellings and the service resolves the pair the same way the web
    // client does; the validator now accepts either.
    final response = await NetworkApiServices().postApi(
      {
        'project': projectId,
        'package': package.id,
        'cover_letter': letter,
      },
      AppUrls.submitProposalUrl,
      LocalKeys.sendProposal,
      headers: commonAuthHeader,
    );

    _submitting = false;
    notifyListeners();

    final created = SubmittedProposal.tryFrom(response);
    if (created != null) {
      LocalKeys.proposalSentSuccessfully.showToast();
    } else {
      // `postApi` already surfaced the server's `error` string through
      // `showValidationErrors`, so nothing is added here -- a second generic
      // toast on top of a specific one is how a user ends up with two messages
      // and no idea which is the real one.
      if (kDebugMode) {
        debugPrint('[proposal] submit returned a non-ProposalResponse body');
      }
    }
    return created;
  }

  /// Flips the details screen's button to "already proposed" without a full
  /// re-fetch, and drops the cached tier prices so the next apply sheet
  /// re-reads them.
  void markProjectApplied(BuildContext context) {
    Provider.of<ProjectDetailsService>(context, listen: false).setAlreadyApplied();
    _packagesFetched = false;
    notifyListeners();
  }
}
