import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/services/contract_details_service.dart';
import 'package:webofwork/utils/components/custom_button.dart';
import 'package:webofwork/utils/components/field_label.dart';
import 'package:webofwork/utils/components/star_rating.dart';

/// The freelancer's review of the client: a 1–5 star picker and a public
/// comment, matching the reference modal (which defaults to 5 stars).
///
/// `POST /api/reviews` with `{ contract, rating, comment, review_type:
/// "client" }`; the server 201s the created review and the service folds it
/// into the contract as the "Your review for client" card.
Future<bool?> showSubmitReviewSheet(
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
      child: _SubmitReviewForm(contractId: contractId),
    ),
  );
}

class _SubmitReviewForm extends StatefulWidget {
  const _SubmitReviewForm({required this.contractId});

  final int contractId;

  @override
  State<_SubmitReviewForm> createState() => _SubmitReviewFormState();
}

class _SubmitReviewFormState extends State<_SubmitReviewForm> {
  final _formKey = GlobalKey<FormState>();
  final _commentController = TextEditingController();

  // The reference modal initialises the star picker at 5.
  int _rating = 5;
  bool _submitting = false;

  @override
  void dispose() {
    _commentController.dispose();
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
            Text(LocalKeys.rateYourExperience, style: context.titleMedium?.bold6),
            20.toHeight,
            FieldLabel(label: LocalKeys.overallExperience, isRequired: true),
            StarRating(rating: _rating, size: 34, onChanged: _setRating),
            20.toHeight,
            FieldLabel(label: LocalKeys.publicFeedback, isRequired: true),
            TextFormField(
              controller: _commentController,
              maxLines: 4,
              decoration: InputDecoration(
                hintText: LocalKeys.feedbackHint,
              ),
              validator: (v) {
                final text = v?.trim() ?? '';
                if (text.isEmpty) return LocalKeys.feedbackHint;
                return null;
              },
            ),
            24.toHeight,
            SizedBox(
              width: double.infinity,
              child: CustomButton(
                onPressed: _submitting ? () {} : _submit,
                btText: LocalKeys.submitReview,
                isLoading: _submitting,
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _setRating(int rating) => setState(() => _rating = rating);

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    _submitting = true;
    setState(() {});

    final ok = await context.read<ContractDetailsService>().tryCreateReview(
          contractId: widget.contractId,
          rating: _rating,
          comment: _commentController.text,
        );

    if (!mounted) return;
    setState(() => _submitting = false);
    if (ok) Navigator.of(context).pop(true);
  }
}