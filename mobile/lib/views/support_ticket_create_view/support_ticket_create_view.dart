import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/empty_spacer_helper.dart';
import 'package:webofwork/utils/components/field_label.dart';
import 'package:webofwork/utils/components/field_with_label.dart';
import 'package:webofwork/utils/components/navigation_pop_icon.dart';
import 'package:webofwork/view_models/support_tickets_view_model/support_tickets_view_model.dart';

/// Raises a support ticket: `POST /api/support/tickets` with `{subject, message}`.
///
/// The title, priority and department fields were removed with the rest of the
/// template support form -- `CreateSupportTicketRequest` has only `Subject` and
/// `Message`, and the department dropdown was permanently empty because its
/// source route does not exist, so this form could never be submitted.
class SupportTicketCreateView extends StatefulWidget {
  static const routeName = 'support_ticket_create_view';
  const SupportTicketCreateView({super.key});

  @override
  State<SupportTicketCreateView> createState() => _SupportTicketCreateViewState();
}

class _SupportTicketCreateViewState extends State<SupportTicketCreateView> {
  // Per-State so two mounted views never share one GlobalKey.
  final _formKey = GlobalKey<FormState>();
  final stm = SupportTicketViewModel.instance;

  @override
  void initState() {
    super.initState();
    stm.formKey = _formKey;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        leading: const NavigationPopIcon(),
        title: Text(LocalKeys.createTicket),
      ),
      body: Container(
        margin: const EdgeInsets.all(20),
        decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(8), color: context.dProvider.whiteColor),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                FieldWithLabel(
                  label: LocalKeys.subject,
                  hintText: LocalKeys.enterASubject,
                  controller: stm.subjectController,
                  validator: (subject) {
                    if (subject == null || subject.trim().isEmpty) {
                      return LocalKeys.enterASubject;
                    }
                    return null;
                  },
                ),
                EmptySpaceHelper.emptyHeight(12),
                FieldLabel(label: LocalKeys.message),
                TextFormField(
                  maxLines: 5,
                  minLines: 5,
                  controller: stm.messageController,
                  decoration: InputDecoration(
                    hintText: LocalKeys.enterSomeDescription,
                  ),
                  validator: (message) {
                    if (message == null || message.trim().isEmpty) {
                      return LocalKeys.enterSomeDescription;
                    }
                    return null;
                  },
                ),
                EmptySpaceHelper.emptyHeight(24),
                ValueListenableBuilder(
                  valueListenable: stm.loading,
                  builder: (context, loading, child) => CustomButton(
                    onPressed: () => stm.createTicket(context),
                    btText: LocalKeys.createTicket,
                    isLoading: loading,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
