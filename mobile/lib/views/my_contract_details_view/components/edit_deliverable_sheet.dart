import 'dart:io';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/contract_model.dart';
import 'package:webofwork/services/contract_details_service.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/field_label.dart';

import '../../../app_static_values.dart';
import 'package:webofwork/utils/components/attachment_select.dart';

/// Corrects a submitted deliverable before the contract leaves `active`/
/// `submitted`.
///
/// `PATCH /api/deliverables/{id}` takes **multipart/form-data** and applies
/// only the fields present, so an unchanged field is simply left out. A chosen
/// file replaces the uploaded one; a cleared link removes it. The reference
/// modal mirrors this exactly (title, notes, link, optional file).
Future<bool?> showEditDeliverableSheet(
  BuildContext context, {
  required ContractDeliverable deliverable,
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
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: _EditDeliverableForm(deliverable: deliverable),
    ),
  );
}

class _EditDeliverableForm extends StatefulWidget {
  const _EditDeliverableForm({required this.deliverable});

  final ContractDeliverable deliverable;

  @override
  State<_EditDeliverableForm> createState() => _EditDeliverableFormState();
}

class _EditDeliverableFormState extends State<_EditDeliverableForm> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _titleController;
  late final TextEditingController _notesController;
  late final TextEditingController _linkController;
  final ValueNotifier<File?> _file = ValueNotifier<File?>(null);

  bool _saving = false;

  @override
  void initState() {
    super.initState();
    final d = widget.deliverable;
    _titleController = TextEditingController(text: d.title);
    _notesController = TextEditingController(text: d.notes ?? '');
    _linkController = TextEditingController(text: d.linkUrl ?? '');
  }

  @override
  void dispose() {
    _file.dispose();
    _titleController.dispose();
    _notesController.dispose();
    _linkController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(LocalKeys.editDeliverable, style: context.titleMedium?.bold6),
            20.toHeight,
            FieldLabel(label: LocalKeys.title),
            TextFormField(
              controller: _titleController,
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
              maxMBSize: 20,
              allowedExtensions: supportedWorkFiles,
            ),
            12.toHeight,
            FieldLabel(label: LocalKeys.linkUrl),
            TextFormField(
              controller: _linkController,
              keyboardType: TextInputType.url,
              autocorrect: false,
              decoration: InputDecoration(
                hintText: LocalKeys.linkUrlHint,
              ),
            ),
            24.toHeight,
            SizedBox(
              width: double.infinity,
              child: CustomButton(
                onPressed: _saving ? () {} : _save,
                btText: LocalKeys.saveChanges,
                isLoading: _saving,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _save() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    _saving = true;
    setState(() {});

    final ok = await context.read<ContractDetailsService>().tryUpdateDeliverable(
          deliverableId: widget.deliverable.id,
          title: _titleController.text,
          notes: _notesController.text,
          linkUrl: _linkController.text,
          file: _file.value,
        );

    if (!mounted) return;
    setState(() => _saving = false);
    if (ok) Navigator.of(context).pop(true);
  }
}