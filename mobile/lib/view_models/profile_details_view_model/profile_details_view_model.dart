import 'package:flutter/material.dart';

/// Scroll controller for the profile-details screen.
///
/// The two confirmation-dialog helpers that used to live here
/// (`tryChangingStatus`, `tryProjectStatusChange`) drove write endpoints that
/// do not exist on this API: a `check_work_availability` POST to a GET-only
/// route, and a `project_on_off` POST for a project catalogue clients do not
/// have. Availability is edited via `PATCH /api/profile/freelancer/me` on the
/// Profile Edit screen. The `viewAsClient` / `skillEditing` / `editPrice` /
/// `priceController` notifiers belonged to inline-edit affordances in this view
/// and are gone with them.
class ProfileDetailsViewModel {
  final ScrollController controller = ScrollController();

  ProfileDetailsViewModel._init();
  static ProfileDetailsViewModel? _instance;
  static ProfileDetailsViewModel get instance {
    _instance ??= ProfileDetailsViewModel._init();
    return _instance!;
  }

  static bool get dispose {
    _instance = null;
    return true;
  }
}
