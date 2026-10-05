import 'dart:io';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/contract_details_service.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/field_label.dart';

import '../../../app_static_values.dart';
import 'package:webofwork/utils/components/attachment_select.dart';

/// Asks the server for 20 MB to match its own `[RequestSizeLimit(20 * 1024 * 1024)]`
/// on `POST /api/contracts/{id}/submit-work`.
///
/// The picker previously allowed 100 MB, so picking a 60 MB file passed the
/// client-side check and was then rejected by the server after the whole upload
/// had crossed the network.
const _maxUploadMb = 20;

/// Submits work against a contract.
///
/// `POST /api/contracts/{id}/submit-work` (aliased to
/// `POST /api/deliverables/contract/{contractId}`) takes **multipart/form-data**:
/// `title`, `notes`, optional `link_url`, `deliverable_type`, plus a `file` part
/// which the server uploads to Cloudinary.
///
/// Replaces `WorkSubmitView`, which was built around a `milestoneId` this API has
/// no concept of and posted to the template's `user/upload/work` through a
/// rich-text editor. Deliverable `notes` are plain text, so the editor is gone
/// and the form is three fields.
Future<bool?> showSubmitDeliverableSheet(
  BuildContext context, {
  required int contractId,
}) {
  return showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: context.dProvider.whiteColor,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.only(
        topLeft: Radius.circular(20),
        topRight: Radius.circular(20),
      ),
    ),
    builder: (context) => Padding(
      // Lifts the sheet above the keyboard; without this the fields are hidden
      // behind it and the submit button cannot be reached.
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: _SubmitDeliverableForm(contractId: contractId),
    ),
  );
}

class _SubmitDeliverableForm extends StatefulWidget {
  const _SubmitDeliverableForm({required this.contractId});

  final int contractId;

  @override
  State<_SubmitDeliverableForm> createState() => _SubmitDeliverableFormState();
}

class _SubmitDeliverableFormState extends State<_SubmitDeliverableForm> {
  final _formKey = GlobalKey<FormState>();
  final _titleController = TextEditingController();
  final _notesController = TextEditingController();
  final _linkController = TextEditingController();

  // A `ValueNotifier` rather than widget state because `AttachmentSelect` is a
  // `StatelessWidget` that takes the picker as a notifier.
  final ValueNotifier<File?> _file = ValueNotifier<File?>(null);

  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    // Once a file is attached, a typed link is redundant -- the server treats
    // them as alternatives (`deliverable_type` is `file` or `link`).
    _file.addListener(_onFileChanged);
  }

  void _onFileChanged() {
    if (_file.value != null) _linkController.clear();
    setState(() {});
  }

  @override
  void dispose() {
    _file.removeListener(_onFileChanged);
    _file.dispose();
    _titleController.dispose();
    _notesController.dispose();
    _linkController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final hasFile = _file.value != null;
    final hasLink = _linkController.text.trim().isNotEmpty;

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(LocalKeys.submitWork, style: context.titleMedium?.bold6),
            20.toHeight,
            FieldLabel(label: LocalKeys.title),
            TextFormField(
              controller: _titleController,
              textInputAction: TextInputAction.next,
              onChanged: (_) => setState(() {}),
              decoration: InputDecoration(
                hintText: LocalKeys.workNotesRequired,
              ),
            ),
            16.toHeight,
            FieldLabel(label: LocalKeys.notes),
            TextFormField(
              controller: _notesController,
              maxLines: 4,
              decoration: InputDecoration(
                hintText: LocalKeys.enterDescription,
              ),
            ),
            16.toHeight,
            AttachmentSelect(
              selectedAttachment: _file,
              maxMBSize: _maxUploadMb,
              allowedExtensions: supportedWorkFiles,
            ),
            16.toHeight,
            FieldLabel(label: LocalKeys.linkUrl),
            TextFormField(
              controller: _linkController,
              enabled: !hasFile,
              keyboardType: TextInputType.url,
              autocorrect: false,
              onChanged: (_) {
                // A link makes the file unnecessary, so clear the other one.
                if (_linkController.text.trim().isNotEmpty && _file.value != null) {
                  _file.value = null;
                }
                setState(() {});
              },
              decoration: InputDecoration(
                hintText: LocalKeys.linkUrlHint,
                helperText: hasFile ? LocalKeys.attachFileOrLink : null,
              ),
            ),
            24.toHeight,
            SizedBox(
              width: double.infinity,
              child: CustomButton(
                onPressed: _submitting
                    ? () {}
                    : () => _submit(hasFile: hasFile, hasLink: hasLink),
                btText: LocalKeys.submitWork,
                isLoading: _submitting,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _submit({required bool hasFile, required bool hasLink}) async {
    if (_formKey.currentState?.validate() ?? false) {
      if (!hasFile && !hasLink) {
        // The server answers "Attach a file or provide a link." with a 400. The
        // hint alone was not enough -- users tapped submit and got a red toast
        // with no field marked -- so it is caught here with a field-level
        // message.
        LocalKeys.attachFileOrLink.showToast();
        return;
      }

      final title = _titleController.text.trim();
      if (title.isEmpty && _notesController.text.trim().isEmpty) {
        LocalKeys.workNotesRequired.showToast();
        return;
      }

      _submitting = true;
      setState(() {});

      final ok = await context.read<ContractDetailsService>().trySubmitOrder(
            widget.contractId,
            file: _file.value,
            description: _notesController.text.trim(),
            title: title,
            linkUrl: hasLink ? _linkController.text.trim() : null,
          );

      if (!mounted) return;
      setState(() => _submitting = false);
      if (ok) {
        // The service has already merged the new deliverable into its contract
        // and notified its listeners; popping is all that is left.
        Navigator.of(context).pop(true);
      }
    }
  }
}
