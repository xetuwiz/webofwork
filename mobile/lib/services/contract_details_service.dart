import 'dart:io';

import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:webofwork/helper/constant_helper.dart';
import 'package:webofwork/helper/extension/string_extension.dart';

import '../data/network/network_api_services.dart';
import '../helper/app_urls.dart';
import '../helper/local_keys.g.dart';
import '../models/contract_model.dart';

/// A single contract, for the details screen.
///
/// Backed by `GET /api/contracts/{id}`, which returns a **bare**
/// `ContractResponse` -- not a wrapper. `OrderDetailsModel.fromJson` used to
/// branch on `json.containsKey("projectTitle")`, a camelCase key this API never
/// sends, so the branch never fired and the screen fell through to
/// `json["order_details"]` (also absent). The whole screen was empty.
class ContractDetailsService with ChangeNotifier {
  Contract? _contract;
  Contract? get contract => _contract;

  bool _loading = false;
  bool get isLoading => _loading;

  String _token = '';
  String _id = '';

  /// The contract id currently loaded, as a string.
  String get contractId => _id;

  /// Re-fetch when the screen is pointed at a different contract, or when the
  /// stored JWT no longer matches the live one (i.e. a different user signed in).
  bool shouldAutoFetch(dynamic id) => id.toString() != _id || _token.isInvalid;

  Future<void> fetchOrderDetails({required dynamic orderId}) async {
    final contractId = asInt(orderId);
    if (contractId == 0) {
      // A notification can deep-link here with a null or non-numeric id; an
      // unparseable one must not become a request for contract 0.
      _contract = null;
      notifyListeners();
      return;
    }

    _token = getToken;
    _id = contractId.toString();
    _loading = true;
    notifyListeners();

    final responseData = await NetworkApiServices().getApi(
      '${AppUrls.contractDetailUrl}$contractId',
      LocalKeys.orderDetails,
      headers: commonAuthHeader,
    );

    _loading = false;
    if (responseData is Map) {
      _contract = Contract.fromJson(responseData);
    } else {
      _contract = null;
    }
    notifyListeners();
  }

  /// Review and sign an offered contract.
  ///
  /// `POST /api/contracts/{id}/accept` -> **204 No Content**, so there is no
  /// response object to read. `returnResponse` maps 204 to an empty map rather
  /// than `null`, which is what makes the `!= null` check below meaningful.
  ///
  /// The body is `signature` / `signature_type` in **snake_case**. It used to
  /// send `Signature` / `SignatureType`, which `Program.cs`'s
  /// `PropertyNamingPolicy = SnakeCaseLower` silently dropped -- so
  /// `AcceptContractRequest.Signature` was empty, the server stored a blank
  /// signature, and the contract still flipped to Active. The server now rejects
  /// that case with 400, and the correct keys are sent here.
  Future<bool> tryAcceptOrder({required dynamic orderId}) async {
    final contractId = asInt(orderId);
    if (contractId == 0) return false;

    final responseData = await NetworkApiServices().postApi(
      {
        'signature': 'accepted',
        'signature_type': 'type',
      },
      '${AppUrls.contractDetailUrl}$contractId/accept',
      LocalKeys.acceptOrder,
      headers: acceptJsonAuthHeader,
    );

    if (responseData == null) return false;

    _contract = _contract?.copyWith(
      status: Contract.statusActive,
      freelancerHasSigned: true,
      freelancerSignedAt: DateTime.now(),
    );
    notifyListeners();
    return true;
  }

  /// Move the contract to a new status.
  ///
  /// `PATCH /api/contracts/{id}/status` body `{ "status": "cancelled" }`.
  ///
  /// This used to POST `{order_id, cancel_or_decline_order}` to
  /// `/api/contracts/`, a template shape. That route is
  /// `[HttpPost] [AllowAnonymous] => 405 "Use payment flow to create contracts"`,
  /// so cancel **always** failed.
  ///
  /// The route was also `[Authorize(Policy = "RequireClient")]`, so a freelancer
  /// got 403 before the service could even check the transition. The policy has
  /// been dropped, because `ContractService.UpdateStatusAsync` already looks up
  /// both parties and throws for a non-participant. `ContractStateMachine`
  /// permits `Offered|Active -> Cancelled` and `Active|Submitted -> Disputed`
  /// from either side, which is what makes this reachable at all.
  Future<bool> tryUpdateStatus({
    required dynamic orderId,
    required String status,
    String? paymentStatus,
  }) async {
    final contractId = asInt(orderId);
    if (contractId == 0) return false;

    final body = <String, dynamic>{'status': status};
    if (paymentStatus != null) body['payment_status'] = paymentStatus;

    final responseData = await NetworkApiServices().patchApi(
      body,
      '${AppUrls.contractDetailUrl}$contractId/status',
      LocalKeys.cancelOrder,
      headers: acceptJsonAuthHeader,
    );

    if (responseData == null) return false;

    _contract = _contract?.copyWith(status: status);
    notifyListeners();
    return true;
  }

  /// Cancel, keeping the old call sites working.
  Future<bool> tryCancelOrder({
    required dynamic orderId,
    required String action,
  }) =>
      // The old API distinguished "cancel" from "decline" by an action string.
      // A contract has no decline state; declining an *invitation* is
      // `PATCH /api/projects/invitations/{id}/update-status`. Both spellings
      // therefore mean the same thing here: cancel the contract.
      tryUpdateStatus(orderId: orderId, status: Contract.statusCancelled);

  /// Submit work against a live contract.
  ///
  /// `POST /api/contracts/{id}/submit-work` (aliased to
  /// `POST /api/deliverables/contract/{contractId}`) takes **multipart/form-data**
  /// with `title`, `notes`, optional `link_url`, `deliverable_type`, and a `file`
  /// part. The file is uploaded to Cloudinary server-side (20 MB cap).
  ///
  /// The title used to be the constant `"Work Submission"` for every delivery,
  /// so the deliverables list was a wall of identical rows.
  Future<bool> trySubmitOrder(
    int contractId, {
    required File? file,
    required String description,
    String title = '',
    String? linkUrl,
  }) async {
    final hasFile = file != null && file.path.isNotEmpty;
    final hasLink = linkUrl != null && linkUrl.trim().isNotEmpty;

    // The server rejects with "Attach a file or provide a link." when neither
    // is present; checking here turns that into a specific message instead of
    // a round trip.
    if (!hasFile && !hasLink) {
      LocalKeys.selectFile.showToast();
      return false;
    }

    // Falls back to the notes, then to a generic label, so a delivery is never
    // submitted with an empty title. The old code sent the constant
    // "Work Submission" for every delivery, which made the deliverables list a
    // wall of identical rows.
    final resolvedTitle = title.trim().isNotEmpty
        ? title.trim()
        : description.trim().isNotEmpty
            ? description.trim()
            : 'Work submitted';

    final request = http.MultipartRequest(
      'POST',
      Uri.parse('${AppUrls.deliverablesUrl}contract/$contractId'),
    )
      ..fields['title'] = resolvedTitle
      ..fields['notes'] = description
      ..fields['deliverable_type'] = hasFile ? 'file' : 'link'
      ..headers.addAll(acceptJsonAuthHeader);

    if (linkUrl != null && linkUrl.trim().isNotEmpty) {
      request.fields['link_url'] = linkUrl.trim();
    }
    if (file != null && file.path.isNotEmpty) {
      LocalKeys.uploadingFileMightTakeSomeTime.showToast();
      request.files.add(await http.MultipartFile.fromPath('file', file.path));
    }

    final responseData =
        await NetworkApiServices().postWithFileApi(request, LocalKeys.submitWork);

    if (responseData is Map) {
      // Fold the new deliverable into the loaded contract so the list updates
      // without a second fetch.
      final created = ContractDeliverable.fromJson(responseData);
      final current = _contract;
      if (current != null) {
        _contract = current.copyWith(
          deliverables: [...current.deliverables, created],
        );
      }
      LocalKeys.orderSubmittedSuccessfully.showToast();
      notifyListeners();
      return true;
    }
    return false;
  }

  /// Accept or reject a change request on submitted work.
  ///
  /// `POST /api/revisions/{id}/action` body `{ "accept": bool,
  /// "rejection_message": string? }` -> **204 No Content**.
  ///
  /// This is the one revision action the freelancer owns:
  /// `RevisionsController` puts `[Authorize(Policy = "RequireFreelancer")]` on
  /// `{id}/action` and `RequireClient` on `contract/{contractId}`
  /// (`request-revision`). So the client asks for changes and the freelancer
  /// resolves them -- the reverse of the template's flow, which showed the
  /// freelancer a `revision_left` counter and let them request a revision.
  Future<bool> tryResolveRevision({
    required int contractId,
    required int revisionId,
    required bool accept,
    String? rejectionMessage,
  }) async {
    final body = <String, dynamic>{'accept': accept};
    if (!accept && (rejectionMessage ?? '').trim().isNotEmpty) {
      body['rejection_message'] = rejectionMessage!.trim();
    }

    final responseData = await NetworkApiServices().postApi(
      body,
      AppUrls.revisionActionUrl(revisionId),
      LocalKeys.revisions,
      headers: acceptJsonAuthHeader,
    );

    if (responseData == null) return false;

    // Fold the outcome into the loaded contract so the list updates without a
    // refetch.
    final current = _contract;
    if (current != null) {
      final index = current.revisions.indexWhere((r) => r.id == revisionId);
      if (index >= 0) {
        final next = [...current.revisions];
        next[index] = ContractRevision(
          id: next[index].id,
          contractId: next[index].contractId,
          userId: next[index].userId,
          reason: next[index].reason,
          status: accept ? ContractRevision.accepted : ContractRevision.rejected,
          rejectionMessage: accept
              ? null
              : (rejectionMessage?.trim().isNotEmpty ?? false)
                  ? rejectionMessage!.trim()
                  : next[index].rejectionMessage,
          requestedAt: next[index].requestedAt,
          resolvedAt: DateTime.now(),
        );
        _contract = current.copyWith(revisions: next);
      }
    }
    notifyListeners();
    return true;
  }

  /// Submit the freelancer's review of the client.
  ///
  /// `POST /api/reviews` body `{ contract, rating, comment, review_type }`.
  /// The contract id is sent as **`contract`**, the alias the reference client
  /// uses; the controller normalises it into `ContractId` before validating.
  /// `review_type: "client"` selects the "review of the client" branch in
  /// `ReviewService` (the enum is derived from the caller's role, so the value
  /// is documentation more than input).
  ///
  /// The server 201s with the created `ReviewResponse`, which is folded into
  /// the loaded contract as the `freelancer_review` card so the screen updates
  /// without a refetch.
  Future<bool> tryCreateReview({
    required int contractId,
    required int rating,
    required String comment,
  }) async {
    final responseData = await NetworkApiServices().postApi(
      {
        'contract': contractId,
        'rating': rating,
        'comment': comment.trim(),
        'review_type': 'client',
      },
      AppUrls.reviewsUrl,
      LocalKeys.submitReview,
      headers: acceptJsonAuthHeader,
    );

    if (responseData is Map) {
      _contract = _contract?.copyWith(
        freelancerReview: ContractReview.fromJson(responseData),
      );
      LocalKeys.reviewSubmitted.showToast();
      notifyListeners();
      return true;
    }
    return false;
  }

  /// Raise a dispute against a contract.
  ///
  /// `POST /api/contracts/dispute` body `{ contract_id, reason, description,
  /// evidence_urls }`. The reason is the reference wire label (snake_case, or
  /// the literal `Unlimited Revisions`); the server's custom converter
  /// accepts exactly those. The route is `[Authorize]` with no client-only
  /// policy, so a freelancer can open it -- the reference posts both the id in
  /// the body and a path parameter that this alias route does not consume.
  ///
  /// `evidence_urls` are links. On the web the reference uploads files to
  /// Cloudinary and pastes the returned URLs; mobile has no Cloudinary signing
  /// credentials, so the sheet collects URL entries (a file, screenshot or
  /// video hosted anywhere).
  ///
  /// The 201 `DisputeResponse` is folded into the contract (status -> disputed
  /// plus the dispute banner) so the screen reflects the change immediately.
  Future<bool> tryRaiseDispute({
    required int contractId,
    required String reason,
    required String description,
    List<String> evidenceUrls = const [],
  }) async {
    final responseData = await NetworkApiServices().postApi(
      {
        'contract_id': contractId,
        'contract': contractId,
        'reason': reason,
        'description': description.trim(),
        'evidence_urls':
            evidenceUrls.map((e) => e.trim()).where((e) => e.isNotEmpty).toList(),
      },
      AppUrls.disputesUrl,
      LocalKeys.raiseDispute,
      headers: acceptJsonAuthHeader,
    );

    if (responseData is Map) {
      _contract = _contract?.copyWith(
        status: Contract.statusDisputed,
        dispute: ContractDispute.fromJson(responseData),
      );
      LocalKeys.disputeSubmitted.showToast();
      notifyListeners();
      return true;
    }
    return false;
  }

  /// Correct a submitted deliverable while the contract is still running.
  ///
  /// `PATCH /api/deliverables/{id}` takes **multipart/form-data** (`title`,
  /// `notes`, `link_url`, optional `file`). The server's `UpdateDeliverableAsync`
  /// applies only the fields present, and refuses once the contract leaves
  /// `active|submitted` (`EnsureMutable`), which is exactly the gate
  /// `Contract.canManageDeliverables` mirrors on the tile.
  Future<bool> tryUpdateDeliverable({
    required int deliverableId,
    String title = '',
    String? notes,
    String? linkUrl,
    File? file,
  }) async {
    final request = http.MultipartRequest(
      'PATCH',
      Uri.parse('${AppUrls.deliverablesUrl}$deliverableId'),
    )..headers.addAll(acceptJsonAuthHeader);

    if (title.trim().isNotEmpty) request.fields['title'] = title.trim();
    if (notes != null) request.fields['notes'] = notes;
    if (linkUrl != null) request.fields['link_url'] = linkUrl.trim();
    if (file != null && file.path.isNotEmpty) {
      request.files.add(await http.MultipartFile.fromPath('file', file.path));
    }

    final responseData =
        await NetworkApiServices().postWithFileApi(request, LocalKeys.editDeliverable);

    if (responseData is Map) {
      final updated = ContractDeliverable.fromJson(responseData);
      final current = _contract;
      if (current != null) {
        final index = current.deliverables.indexWhere((d) => d.id == updated.id);
        final next = [...current.deliverables];
        if (index >= 0) {
          next[index] = updated;
        } else {
          next.add(updated);
        }
        _contract = current.copyWith(deliverables: next);
      }
      LocalKeys.deliverableUpdated.showToast();
      notifyListeners();
      return true;
    }
    return false;
  }

  /// Remove a submitted deliverable.
  ///
  /// `DELETE /api/deliverables/{id}` -> **204 No Content**, mapped to an empty
  /// object by `returnResponse`, so the `!= null` check is what counts. The
  /// folded list drops the row locally, again avoiding a refetch.
  Future<bool> tryDeleteDeliverable(int deliverableId) async {
    final responseData = await NetworkApiServices().deleteApi(
      '${AppUrls.deliverablesUrl}$deliverableId',
      LocalKeys.deleteDeliverable,
      headers: acceptJsonAuthHeader,
    );

    if (responseData == null) return false;

    final current = _contract;
    if (current != null) {
      _contract = current.copyWith(
        deliverables: current.deliverables
            .where((d) => d.id != deliverableId)
            .toList(growable: false),
      );
    }
    LocalKeys.deliverableDeleted.showToast();
    notifyListeners();
    return true;
  }

  void clear() {
    _contract = null;
    _id = '';
    notifyListeners();
  }
}
