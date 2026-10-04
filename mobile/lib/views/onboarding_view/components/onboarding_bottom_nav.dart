import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:badges/badges.dart' as badges;
import '../../../helper/local_keys.g.dart';
import '../../../helper/svg_assets.dart';
import '/helper/extension/context_extension.dart';
import '/helper/extension/string_extension.dart';
import '/view_models/onboarding_view_model/onboarding_view_model.dart';

/// Bottom navigation bar for the freelancer shell.
///
/// The tab order is fixed by `OnboardingView`'s `widgets` list, and these
/// indices must match it:
///
///   0 → DashboardView
///   1 → HomeView (Find Projects)
///   2 → MyContractsView (Contracts)
///   3 → ProfileView
///
/// Chats and notifications are **not** tabs. They are reached from the
/// dashboard's app-bar actions and the drawer, which is why the shell is four
/// tabs wide even though the old template shell had five. (An earlier version of
/// this comment listed ChatListView and NotificationListView as tabs 1 and 4 --
/// neither has ever been in `widgets`, so the comment described a layout that
/// did not exist.)
class OnboardingNavBar extends StatelessWidget {
  const OnboardingNavBar({super.key});

  @override
  Widget build(BuildContext context) {
    final ov = OnboardingViewModel.instance;
    return ValueListenableBuilder(
        valueListenable: ov.currentIndex,
        builder: (context, value, child) => Container(
              decoration: BoxDecoration(
                color: context.dProvider.whiteColor,
                border:
                    Border(top: BorderSide(color: context.dProvider.black8, width: 2)),
              ),
              // The app is edge-to-edge (targetSdk 36 forces it on Android 15+,
              // and `windowOptOutEdgeToEdgeEnforcement` is ignored at that
              // level), so the system navigation bar is drawn *over* the bottom
              // of the app. This bar is a fixed 72px `Container` with no inset
              // handling, which put the gesture pill / 3-button bar on top of
              // the lower part of the tab row -- taps there went to the system
              // and the home-area swipe was stolen, so tabs looked unresponsive.
              //
              // `SafeArea` sits *inside* the decorated Container on purpose:
              // the white background and top border still run edge-to-edge
              // behind the system bar (the modern look), while the interactive
              // row is lifted clear of it.
              child: SafeArea(
                top: false,
                child: FittedBox(
                  child: SizedBox(
                    height: 72,
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceAround,
                      children: [
                        navBarItem(context, 'Dashboard', SvgAssets.home,
                            SvgAssets.homeBold, 0, ov),
                        navBarItem(context, 'Find Projects', SvgAssets.search,
                            SvgAssets.search, 1, ov),
                        navBarItem(context, LocalKeys.myOrder, SvgAssets.clipboardText,
                            SvgAssets.clipboardTextBold, 2, ov),
                        navBarItem(context, LocalKeys.profile, SvgAssets.user,
                            SvgAssets.userBold, 3, ov),
                      ],
                    ),
                  ),
                ),
              ),
            ));
  }

  navBarItem(BuildContext context, String label, String iconNormal, String iconFilled,
      int index, ov,
      {badgeCount = 0}) {
    final selected = index == ov.currentIndex.value;
    return InkWell(
      onTap: () {
        ov.setNavIndex(index);
      },
      child: Container(
        height: 32,
        alignment: Alignment.center,
        margin: const EdgeInsets.symmetric(horizontal: 8),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
        constraints: BoxConstraints(minWidth: selected ? 132 : 0.0),
        decoration: selected
            ? BoxDecoration(
                borderRadius: BorderRadius.circular(24),
                color: context.dProvider.primary10,
                border: Border.all(color: context.dProvider.primaryColor, width: 2),
              )
            : null,
        child: Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          (selected
              ? iconFilled.toSVGSized(20)
              : badges.Badge(
                  position: badges.BadgePosition.topEnd(),
                  badgeContent: Text(
                    '$badgeCount',
                    style:
                        context.titleSmall?.copyWith(color: context.dProvider.whiteColor),
                  ),
                  showBadge: badgeCount > 0,
                  child: SizedBox(
                    height: 72,
                    child: iconNormal.toSVGSized(20),
                  ))),
          if (selected) 4.toWidth,
          if (selected)
            Text(
              label,
              style: context.titleSmall
                  ?.copyWith(color: context.dProvider.primaryColor)
                  .bold6,
            )
        ]),
      ),
    );
  }
}
