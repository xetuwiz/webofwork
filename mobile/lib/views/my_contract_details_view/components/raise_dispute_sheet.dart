import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/contract_details_service.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/field_label.dart';

/// A dispute reason as the reference dropdown lists it: the **wire value**
/// (`scope_creep`, or the literal `Unlimited Revisions`) paired with a
/// readable label. The server's `DisputeReasonJsonConverter` accepts exactly
/// these wire values.
/// The `value` half stays hardcoded on purpose -- it is a wire identifier, not
/// copy, and the server's alias table is keyed on it. The `label` half goes
/// through [LocalKeys]: it is user-facing, and these were raw English strings in
/// an app whose every other string is localizable.
typedef DisputeReasonOption = ({String value, String label});

/// `final`, not `const`: [LocalKeys] resolves through `.tr()`, which is a runtime
/// lookup, so this list cannot be a compile-time constant any more.
final disputeReasonOptions = <DisputeReasonOption>[
  (value: 'scope_creep', label: LocalKeys.scopeCreep),
  (value: 'no_feedback', label: LocalKeys.noFeedback),
  // Not snake_case, and not a mistake: the reference client sends this literal
  // and `DisputeReasonJsonConverter` accepts it as a documented alias.
  (value: 'Unlimited Revisions', label: LocalKeys.unlimitedRevisions),
  (value: 'poor_quality', label: LocalKeys.poorQuality),
  (value: 'missed_deadline', label: LocalKeys.missedDeadline),
  (value: 'incomplete_work', label: LocalKeys.incompleteWork),
  (value: 'communication_issue', label: LocalKeys.communicationIssue),
  (value: 'other', label: LocalKeys.other),
];

/// Raises a dispute against a contract.
///
/// `POST /api/contracts/dispute` with `contract_id`, `reason`, `description`
/// and `evidence_urls`. The description must be at least 10 characters — the
/// server's `CreateDisputeValidator` enforces it, so the form validates the
/// same rule up front. Evidence is collected as URLs (the reference uploads
/// files to Cloudinary on the web and pastes the results; mobile has no
/// signing credentials, so a hosted link is the honest equivalent).
///
/// Reasons are the reference's own tuples including the literal
/// `Unlimited Revisions` member, because the server's converter mirrors that
/// label exactly.
Future<bool?> showRaiseDisputeSheet(
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
      padding: EdgeInsets.only(
        bottom: MediaQuery.of(context).viewInsets.bottom,
      ),
      child: _RaiseDisputeForm(contractId: contractId),
    ),
  );
}

class _RaiseDisputeForm extends StatefulWidget {
  const _RaiseDisputeForm({required this.contractId});

  final int contractId;

  @override
  State<_RaiseDisputeForm> createState() => _RaiseDisputeFormState();
}

class _RaiseDisputeFormState extends State<_RaiseDisputeForm> {
  final _formKey = GlobalKey<FormState>();
  final _descriptionController = TextEditingController();
  final _evidenceControllers = <TextEditingController>[];

  String? _reason;
  bool _submitting = false;

  @override
  void dispose() {
    _descriptionController.dispose();
    for (final c in _evidenceControllers) {
      c.dispose();
    }
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
            Text(LocalKeys.raiseDispute, style: context.titleMedium?.bold6),
            20.toHeight,
            FieldLabel(label: LocalKeys.disputeReason, isRequired: true),
            DropdownButtonFormField<String>(
              initialValue: _reason,
              isExpanded: true,
              decoration: InputDecoration(
                hintText: LocalKeys.selectDisputeReason,
              ),
              items: disputeReasonOptions
                  .map(
                    (r) => DropdownMenuItem<String>(
                      value: r.value,
                      child: Text(
                        r.label,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  )
                  .toList(),
              onChanged: (v) => setState(() => _reason = v),
              validator: (v) => v == null || v.isEmpty
                  ? LocalKeys.selectDisputeReason
                  : null,
            ),
            16.toHeight,
            FieldLabel(label: LocalKeys.disputeDescriptionLabel, isRequired: true),
            TextFormField(
              controller: _descriptionController,
              maxLines: 4,
              decoration: InputDecoration(
                hintText: LocalKeys.disputeDescriptionHint,
              ),
              validator: (v) {
                final text = v?.trim() ?? '';
                if (text.length < 10) return LocalKeys.disputeDescriptionHint;
                return null;
              },
            ),
            16.toHeight,
            FieldLabel(label: LocalKeys.evidence),
            ..._evidenceControllers.asMap().entries.map(
                  (entry) => Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: Row(
                      children: [
                        Expanded(
                          child: TextFormField(
                            controller: entry.value,
                            keyboardType: TextInputType.url,
                            autocorrect: false,
                            decoration: InputDecoration(
                              hintText: LocalKeys.evidenceUrlHint,
                            ),
                          ),
                        ),
                        8.toWidth,
                        IconButton(
                          onPressed: () => setState(() {
                            entry.value.dispose();
                            _evidenceControllers.removeAt(entry.key);
                          }),
                          icon: const Icon(Icons.close_rounded, size: 20),
                        ),
                      ],
                    ),
                  ),
                ),
            TextButton.icon(
              onPressed: () => setState(() {
                _evidenceControllers.add(TextEditingController());
              }),
              icon: const Icon(Icons.add_link_rounded, size: 18),
              label: Text(LocalKeys.addEvidenceUrl),
            ),
            24.toHeight,
            SizedBox(
              width: double.infinity,
              child: CustomButton(
                onPressed: _submitting ? () {} : _submit,
                btText: LocalKeys.submitDispute,
                isLoading: _submitting,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    if (_reason == null) {
      LocalKeys.selectDisputeReason.showToast();
      return;
    }

    _submitting = true;
    setState(() {});

    final ok = await context.read<ContractDetailsService>().tryRaiseDispute(
          contractId: widget.contractId,
          reason: _reason!,
          description: _descriptionController.text,
          evidenceUrls: _evidenceControllers
              .map((c) => c.text)
              .where((t) => t.trim().isNotEmpty)
              .toList(),
        );

    if (!mounted) return;
    setState(() => _submitting = false);
    if (ok) Navigator.of(context).pop(true);
  }
}