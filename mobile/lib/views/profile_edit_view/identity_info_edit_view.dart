import 'package:flutter/material.dart';

import '../../helper/local_keys.g.dart';
import '../../utils/components/field_label.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';

class IdentityInfoEditView extends StatelessWidget {
  static const routeName = 'identity_info_edit_view';
  const IdentityInfoEditView({super.key});
  @override
  Widget build(BuildContext context) {
    return Scaffold(
        appBar: AppBar(
          leading: const NavigationPopIcon(),
          title: Text(LocalKeys.identityInfo),
        ),
        body: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            FieldLabel(label: LocalKeys.identityType),
          ]),
        ));
  }
}
