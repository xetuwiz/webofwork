import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/field_label.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';
import 'package:webofwork/utils/components/pass_field_with_label.dart';
import 'package:webofwork/view_models/delete_account_view_model/delete_account_view_model.dart';

class DeleteAccountView extends StatefulWidget {
  static const routeName = 'delete_account_view';
  const DeleteAccountView({super.key});

  @override
  State<DeleteAccountView> createState() => _DeleteAccountViewState();
}

class _DeleteAccountViewState extends State<DeleteAccountView> {
  // Per-State so two mounted views never share one GlobalKey.
  final _formKey = GlobalKey<FormState>();
  final dam = DeleteAccountViewModel.instance;

  @override
  void initState() {
    super.initState();
    dam.formKey = _formKey;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
        appBar: AppBar(
          leading: const NavigationPopIcon(),
          title: Text(LocalKeys.deleteAccount),
        ),
        body: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Container(
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(8),
                color: context.dProvider.whiteColor),
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  FieldLabel(label: LocalKeys.describeReason),
                  TextFormField(
                    minLines: 4,
                    maxLines: 4,
                    controller: dam.reasonController,
                    decoration: InputDecoration(hintText: LocalKeys.describeReasonDesc),
                    validator: (value) {
                      if ((value?.trim() ?? "").length < 70) {
                        return LocalKeys.provideAtLeastReason;
                      }
                      return null;
                    },
                  ),
                  16.toHeight,
                  PassFieldWithLabel(
                    label: LocalKeys.password,
                    hintText: LocalKeys.enterPassword,
                    controller: dam.passController,
                    valueListenable: dam.passVN,
                  ),
                  20.toHeight,
                  CustomButton(
                      onPressed: () {
                        dam.tryDeletingAccount(context);
                      },
                      btText: LocalKeys.deleteAccount,
                      isLoading: false),
                ],
              ),
            ),
          ),
        ));
  }
}
