import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/proposal_model.dart';
import 'package:webofwork/services/project_details_service.dart';
import 'package:webofwork/services/proposal_service.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/custom_preloader.dart';
import 'package:webofwork/utils/components/field_label.dart';
import 'package:webofwork/utils/components/field_with_label.dart';

/// Apply to a project.
///
/// This is the freelancer's primary action, and it was completely non-functional:
/// the sheet's Apply button body was literally
/// `// TODO: Implement proposal submission in Phase 4`. The fields it collected
/// could not have been sent even with wiring, because none of them exist on the
/// wire contract:
///
/// | Field collected before | Reality                                                               |
/// |-----------------------|-----------------------------------------------------------------------|
/// | `amount`              | `bid_amount` is the **package's** price; the service overwrites it.     |
/// | delivery-time dropdown| `delivery_days` is the **package's** duration; likewise overwritten.    |
/// | `revisions`           | no such field on `CreateProposalRequest`.                              |
/// | file attachment       | no multipart proposal route; `CreateProposalRequest` binds no file.    |
///
/// What the API actually accepts is a **package id plus a cover letter**
/// (`POST /api/projects/create-proposal` with
/// `{project, package, cover_letter}`), so that is what this collects. The
/// money panel is shown read-only from the chosen tier, because those are the
/// numbers that will be stored.
class ApplyProjectPlainSheet extends StatefulWidget {
  /// The project being applied to. Taken as a constructor argument rather than
  /// read out of a singleton, so the sheet cannot be opened against the wrong
  /// project.
  final int projectId;

  const ApplyProjectPlainSheet({super.key, required this.projectId});

  @override
  State<ApplyProjectPlainSheet> createState() => _ApplyProjectPlainSheetState();
}

class _ApplyProjectPlainSheetState extends State<ApplyProjectPlainSheet> {
  // Per-State so two mounted sheets never share one GlobalKey.
  final _formKey = GlobalKey<FormState>();
  final _coverLetter = TextEditingController();

  FreelancerPackage? _selected;
  bool _loading = false;

  @override
  void initState() {
    super.initState();
    _loadPackages();
  }

  @override
  void dispose() {
    _coverLetter.dispose();
    super.dispose();
  }

  Future<void> _loadPackages() async {
    setState(() => _loading = true);
    final service = context.read<ProposalService>();
    await service.fetchMyPackages();
    if (!mounted) return;
    setState(() {
      _loading = false;
      _selected ??= service.packages.defaultTier;
    });
  }

  Future<void> _submit() async {
    FocusScope.of(context).unfocus();
    final package = _selected;
    if (package == null) return;
    if (_formKey.currentState?.validate() != true) return;

    final created = await context.read<ProposalService>().submit(
          projectId: widget.projectId,
          package: package,
          coverLetter: _coverLetter.text,
        );

    if (!mounted) return;
    if (created == null) return;

    // The details screen is behind this sheet and still says "Send proposal";
    // without this the user could immediately tap it again and get the API's
    // "You have already submitted a proposal for this project."
    context.read<ProjectDetailsService>().setAlreadyApplied();
    context.popTrue;
  }

  @override
  Widget build(BuildContext context) {
    final submitting = context.watch<ProposalService>().submitting;

    return PopScope(
      canPop: !submitting,
      child: Container(
        decoration: BoxDecoration(
          borderRadius: const BorderRadius.only(
            topRight: Radius.circular(20),
            topLeft: Radius.circular(20),
          ),
          color: context.dProvider.whiteColor,
        ),
        constraints: BoxConstraints(
          maxHeight: context.height * 0.9,
        ),
        child: SafeArea(
          top: false,
          child: Form(
            key: _formKey,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Align(
                  alignment: Alignment.center,
                  child: Container(
                    height: 4,
                    width: 48,
                    margin: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(12),
                      color: context.dProvider.black7,
                    ),
                  ),
                ),
                Flexible(
                  child: SingleChildScrollView(
                    padding: EdgeInsets.only(
                      left: 20,
                      right: 20,
                      top: 12,
                      bottom: MediaQuery.of(context).viewInsets.bottom < 20
                          ? 20
                          : MediaQuery.of(context).viewInsets.bottom,
                    ),
                    child: _body(submitting),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _body(bool submitting) {
    if (_loading) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 60),
        child: Center(child: CustomPreloader(width: 40)),
      );
    }

    final service = context.watch<ProposalService>();
    if (!service.hasPackages) return _noPackages();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        FieldLabel(label: LocalKeys.chooseServiceTier),
        8.toHeight,
        ...service.packages.all.map(
          (pkg) => Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: _TierCard(
              package: pkg,
              selected: _selected?.id == pkg.id,
              onTap: submitting ? null : () => setState(() => _selected = pkg),
            ),
          ),
        ),
        16.toHeight,
        if (_selected != null) ...[
          _MoneyBreakdown(package: _selected!),
          16.toHeight,
        ],
        FieldWithLabel(
          label: LocalKeys.yourCoverLatter,
          hintText: LocalKeys.writeYourCoverLater,
          textInputAction: TextInputAction.newline,
          minLines: 4,
          maxLines: 10,
          controller: _coverLetter,
          validator: (value) {
            // 10–4000 chars, matching `CreateProposalValidator`'s
            // `MinimumLength(10)` / `MaximumLength(4000)`. The old check only
            // demanded 100+, and never capped the top end, so a pasted essay
            // rejected by the server with a cryptic 400 had no reason shown.
            final text = (value ?? '').trim();
            if (text.length < 10) {
              return LocalKeys.coverLaterMustBeMoreThen;
            }
            if (text.length > 4000) {
              return 'Cover letter must be 4000 characters or fewer.';
            }
            return null;
          },
        ),
        20.toHeight,
        CustomButton(
          onPressed: _selected == null ? null : _submit,
          btText: LocalKeys.sendProposal,
          isLoading: submitting,
        ),
      ],
    );
  }

  /// The API cannot accept a proposal without a package, so this is the state
  /// every brand-new freelancer is in. Point at the fix instead of rendering a
  /// dead form.
  Widget _noPackages() {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 20),
      child: Column(
        children: [
          Icon(Icons.inventory_2_outlined,
              size: 44, color: context.dProvider.warningColor),
          16.toHeight,
          Text(
            LocalKeys.createAPackageFirst,
            textAlign: TextAlign.center,
            style: context.titleMedium,
          ),
          8.toHeight,
          Text(
            LocalKeys.proposalsNeedAPackage,
            textAlign: TextAlign.center,
            style: context.bodyMedium?.copyWith(color: context.dProvider.black6),
          ),
          20.toHeight,
          CustomButton(
            onPressed: _loading ? null : () => _loadPackages(),
            btText: LocalKeys.retry,
            isLoading: false,
          ),
        ],
      ),
    );
  }
}

class _TierCard extends StatelessWidget {
  final FreelancerPackage package;
  final bool selected;
  final VoidCallback? onTap;

  const _TierCard({
    required this.package,
    required this.selected,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: context.dProvider.whiteColor,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: selected ? context.dProvider.primaryColor : context.dProvider.black8,
            width: selected ? 2 : 1,
          ),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              selected ? Icons.radio_button_checked : Icons.radio_button_unchecked,
              size: 20,
              color: selected ? context.dProvider.primaryColor : context.dProvider.black6,
            ),
            12.toWidth,
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    package.label,
                    style: context.titleMedium?.copyWith(
                      color: context.dProvider.primaryColor,
                    ),
                  ),
                  4.toHeight,
                  Text(
                    package.price.toString().cur,
                    style: context.titleLarge,
                  ),
                  if (package.deliveryDays > 0) ...[
                    4.toHeight,
                    Text(
                      '${package.deliveryDays} ${LocalKeys.days}',
                      style: context.bodySmall?.copyWith(color: context.dProvider.black6),
                    ),
                  ],
                  if (package.description.isNotEmpty) ...[
                    8.toHeight,
                    ...package.description.take(3).map(
                          (line) => Padding(
                            padding: const EdgeInsets.only(bottom: 2),
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Icon(Icons.check,
                                    size: 13, color: context.dProvider.black6),
                                6.toWidth,
                                Expanded(
                                  child: Text(
                                    line,
                                    style: context.bodySmall?.copyWith(
                                      color: context.dProvider.black6,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// Read-only money panel.
///
/// These are the figures the API will store: `bid_amount` and `delivery_days`
/// are copied off the selected package by `ProposalService.CreateProposalAsync`.
/// They are displayed rather than edited so the quote cannot disagree with what
/// gets written.
class _MoneyBreakdown extends StatelessWidget {
  final FreelancerPackage package;
  const _MoneyBreakdown({required this.package});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: context.dProvider.black9,
        borderRadius: BorderRadius.circular(16),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            LocalKeys.whatTheClientSees,
            style: context.titleSmall?.copyWith(color: context.dProvider.black6),
          ),
          12.toHeight,
          _Row(
            label: LocalKeys.contractValue,
            value: package.price.toString().cur,
            emphasis: true,
          ),
          6.toHeight,
          _Row(
            label: LocalKeys.deliveryTime,
            value: '${package.deliveryDays} ${LocalKeys.days}',
          ),
        ],
      ),
    );
  }
}

class _Row extends StatelessWidget {
  final String label;
  final String value;
  final bool emphasis;
  const _Row({required this.label, required this.value, this.emphasis = false});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          label,
          style: context.bodyMedium?.copyWith(color: context.dProvider.black6),
        ),
        Text(
          value,
          style: (emphasis ? context.titleMedium : context.bodyMedium)?.copyWith(
            color: emphasis ? context.dProvider.primaryColor : context.dProvider.black5,
          ),
        ),
      ],
    );
  }
}
