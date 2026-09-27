import 'dart:io';

import 'package:flutter/material.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/extension/string_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/models/profile_details_model.dart';
import 'package:webofwork/models/profile_info_model.dart' show resolveMediaUrl;
import 'package:webofwork/services/profile_edit_service.dart';
import 'package:webofwork/utils/components/alerts.dart';
import 'package:webofwork/utils/components/attachment_select.dart';
import 'package:webofwork/utils/components/app_card.dart';
import 'package:webofwork/utils/components/field_with_label.dart';
import 'package:webofwork/view_models/profile_edit_view_model.dart';
import 'package:provider/provider.dart';

/// The portfolio editor: a list of past-work rows with add / edit / delete.
///
/// Portfolio rows are **not** saved by the Edit Profile button. They are not part
/// of `UpdateFreelancerProfileRequest`, so they commit immediately through
/// `POST|PATCH|DELETE /api/profile/freelancer/portfolio[/{id}]`. Putting this
/// section in the same screen as the rest of the form is what makes them
/// discoverable -- previously the app had no way to write them at all, because
/// `PortfolioItem` carried no `id` to address in a PATCH or DELETE.
///
/// The image is either picked from the device -- uploaded to Cloudinary through
/// `POST .../portfolio/upload-image` and stored by the row's `image_url` -- or
/// typed as a URL for an image that is already hosted somewhere.
class PortfolioSection extends StatelessWidget {
  final ProfileEditViewModel pev;
  const PortfolioSection({super.key, required this.pev});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<List<PortfolioItem>>(
      valueListenable: pev.portfolios,
      builder: (context, items, _) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(LocalKeys.portfolio, style: context.titleMedium?.bold6),
                TextButton.icon(
                  onPressed: () => _openEditor(context),
                  icon: const Icon(Icons.add, size: 18),
                  label: Text(LocalKeys.add),
                ),
              ],
            ),
            8.toHeight,
            if (items.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 12),
                child: Text(
                  LocalKeys.noPortfolioItemsYet,
                  style: context.titleSmall
                      ?.copyWith(color: context.dProvider.black5),
                ),
              )
            else
              ...items.map((item) => _ItemCard(
                    pev: pev,
                    item: item,
                    onEdit: () => _openEditor(context, item: item),
                    onDelete: () => _confirmDelete(context, item),
                  )),
          ],
        );
      },
    );
  }

  Future<void> _openEditor(BuildContext context, {PortfolioItem? item}) async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: context.dProvider.whiteColor,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (sheetContext) => _PortfolioEditorSheet(
        pev: pev,
        item: item,
        // The sheet is its own subtree, so the host screen's context is what
        // still resolves the providers.
        hostContext: context,
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context, PortfolioItem item) async {
    final id = item.id;
    // No id means the row cannot be addressed; a delete would 404 and a PATCH
    // would be impossible. Surface it rather than silently doing nothing.
    if (id == null || id.isEmpty) {
      LocalKeys.portfolioItemMissingId.showToast();
      return;
    }

    await Alerts().confirmationAlert(
      context: context,
      title: LocalKeys.delete,
      description: item.title ?? '',
      buttonText: LocalKeys.delete,
      buttonColor: Colors.red,
      onConfirm: () => pev.tryDeletePortfolio(context, id),
    );
  }
}

class _ItemCard extends StatelessWidget {
  final ProfileEditViewModel pev;
  final PortfolioItem item;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  const _ItemCard({
    required this.pev,
    required this.item,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    // Resolved again on display: the row may hold a bare path when it was just
    // typed, and the model only normalises what arrives over the wire.
    final image = resolveMediaUrl(item.imageUrl);

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: AppCard(
        // Denser than the default: this row is a single line of text plus a
        // 72px thumbnail, and the default 20 would make a list of them feel
        // padded out. The radius and border still come from AppCard so it matches
        // every other card on the screen.
        padding: const EdgeInsets.all(12),
        child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: SizedBox(
              width: 72,
              height: 72,
              child: (image == null || image.isEmpty)
                  ? Container(
                      color: context.dProvider.black9,
                      child: Icon(
                        Icons.image_outlined,
                        color: context.dProvider.black5,
                      ),
                    )
                  : Image.network(
                      image,
                      fit: BoxFit.cover,
                      // A dead or non-image URL must not take the row down.
                      errorBuilder: (context, error, stack) => Container(
                        color: context.dProvider.black9,
                        child: Icon(
                          Icons.broken_image_outlined,
                          color: context.dProvider.black5,
                        ),
                      ),
                    ),
            ),
          ),
          12.toWidth,
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  item.title ?? '',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: context.titleSmall?.bold6,
                ),
                if ((item.description ?? '').isNotEmpty) ...[
                  4.toHeight,
                  Text(
                    item.description!,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: context.titleSmall
                        ?.copyWith(color: context.dProvider.black5),
                  ),
                ],
                8.toHeight,
                ValueListenableBuilder<bool>(
                  valueListenable: pev.portfolioBusy,
                  builder: (context, busy, _) => Row(
                    children: [
                      TextButton.icon(
                        onPressed: busy ? null : onEdit,
                        icon: const Icon(Icons.edit_outlined, size: 16),
                        label: Text(LocalKeys.edit),
                      ),
                      TextButton.icon(
                        onPressed: busy ? null : onDelete,
                        icon: Icon(
                          Icons.delete_outline,
                          size: 16,
                          color: Colors.red,
                        ),
                        label: Text(
                          LocalKeys.delete,
                          style: context.titleSmall?.copyWith(color: Colors.red),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
        ),
      ),
    );
  }
}

/// The add / edit form, presented as a bottom sheet.
///
/// Owns its own controllers because it is created and discarded per invocation;
/// seeding them from [item] is what makes it an edit rather than an add.
class _PortfolioEditorSheet extends StatefulWidget {
  final ProfileEditViewModel pev;
  final PortfolioItem? item;

  /// The screen that opened the sheet, used for the service lookup and the
  /// confirmation toasts -- the sheet's own context is a descendant of it and
  /// would work too, but the host keeps the provider scope unambiguous.
  final BuildContext hostContext;

  const _PortfolioEditorSheet({
    required this.pev,
    required this.hostContext,
    this.item,
  });

  @override
  State<_PortfolioEditorSheet> createState() => _PortfolioEditorSheetState();
}

class _PortfolioEditorSheetState extends State<_PortfolioEditorSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _title;
  late final TextEditingController _description;
  late final TextEditingController _imageUrl;

  /// A `ValueNotifier` because `AttachmentSelect` is a `StatelessWidget` that
  /// takes its selection as a notifier.
  final ValueNotifier<File?> _pickedImage = ValueNotifier<File?>(null);

  /// The URL returned by a completed upload, kept so pressing Save a second time
  /// does not push another copy of the same picture to Cloudinary.
  ///
  /// The form deliberately stays open when the row write fails after a successful
  /// upload (the typed title and description must not be lost), so without this
  /// every retry re-uploaded and orphaned the previous asset. Cleared as soon as
  /// a different file is picked.
  String? _uploadedUrl;

  /// Set while the picked image is being uploaded, so the buttons and the
  /// picker cannot fire a second upload.
  bool _uploading = false;

  bool get _isEditing => widget.item != null;

  @override
  void initState() {
    super.initState();
    _title = TextEditingController(text: widget.item?.title ?? '');
    _description = TextEditingController(text: widget.item?.description ?? '');
    _imageUrl = TextEditingController(text: widget.item?.imageUrl ?? '');
    // Picking a file makes the typed URL redundant, so the two are mutually
    // exclusive -- the same rule the deliverable form applies to `link_url`.
    _pickedImage.addListener(_onImagePicked);
  }

  void _onImagePicked() {
    if (_pickedImage.value != null) {
      _imageUrl.clear();
      // A different file, so the previous upload is no longer the right one.
      _uploadedUrl = null;
    }
    setState(() {});
  }

  @override
  void dispose() {
    _pickedImage.removeListener(_onImagePicked);
    _pickedImage.dispose();
    _title.dispose();
    _description.dispose();
    _imageUrl.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_uploading) return;
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final pev = widget.pev;
    final title = _title.text;
    final description = _description.text;

    // An upload failure must abort before the row is written, otherwise the item
    // would be saved with an empty image and the picked file silently dropped.
    var imageUrl = _imageUrl.text.trim();
    final picked = _pickedImage.value;
    if (_uploadedUrl != null) {
      // Already uploaded earlier in this sheet session -- reuse it.
      imageUrl = _uploadedUrl!;
    } else if (picked != null) {
      setState(() => _uploading = true);
      final service = Provider.of<ProfileEditService>(
        widget.hostContext,
        listen: false,
      );
      final uploaded = await service.tryUploadPortfolioImage(picked);
      if (!mounted) return;
      setState(() => _uploading = false);
      if (uploaded == null) {
        // The network layer already toasted; stay open so the form is not lost.
        return;
      }
      _uploadedUrl = uploaded;
      imageUrl = uploaded;
    }

    final id = widget.item?.id;
    final ok = (id == null || id.isEmpty)
        ? await pev.tryAddPortfolio(
            widget.hostContext,
            title: title,
            description: description,
            imageUrl: imageUrl,
          )
        : await pev.tryEditPortfolio(
            widget.hostContext,
            id,
            title: title,
            description: description,
            imageUrl: imageUrl,
          );

    // Stay open on failure so the typed data is not lost; the network layer has
    // already explained why.
    if (ok && mounted) context.popFalse;
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      // Lifts the fields above the keyboard.
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child: Form(
          key: _formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                _isEditing ? LocalKeys.edit : LocalKeys.add,
                style: context.titleLarge?.bold6,
              ),
              16.toHeight,
              FieldWithLabel(
                label: LocalKeys.title,
                hintText: LocalKeys.enterTitle,
                controller: _title,
                textInputAction: TextInputAction.next,
                validator: (value) => (value ?? '').trim().isEmpty
                    ? LocalKeys.fieldIsRequired
                    : null,
              ),
              FieldWithLabel(
                label: LocalKeys.description,
                hintText: LocalKeys.enterDescription,
                controller: _description,
                keyboardType: TextInputType.multiline,
                minLines: 3,
                maxLines: 5,
              ),
              // Picks an image and uploads it through
              // POST .../portfolio/upload-image, which is the same
              // server-side Cloudinary path a deliverable upload uses. The URL
              // field below stays as the alternative for an image that is
              // already hosted somewhere.
              AttachmentSelect(
                selectedAttachment: _pickedImage,
                allowedExtensions: const ['jpg', 'jpeg', 'png', 'webp'],
                // Matches the server's 10 MB ceiling.
                maxMBSize: 10,
                isRequired: _pickedImage.value == null,
              ),
              FieldWithLabel(
                label: LocalKeys.imageUrl,
                hintText: LocalKeys.enterImageUrl,
                controller: _imageUrl,
                keyboardType: TextInputType.url,
                // Only validated when no file was picked -- the picker satisfies
                // the same requirement, and requiring both would be wrong.
                validator: _pickedImage.value != null ? null : _validateImageUrl,
              ),
              16.toHeight,
              ValueListenableBuilder<bool>(
                valueListenable: widget.pev.portfolioBusy,
                builder: (context, busy, _) {
                  final blocked = busy || _uploading;
                  return Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed:
                              blocked ? null : () => context.popFalse,
                          child: Text(LocalKeys.cancel),
                        ),
                      ),
                      12.toWidth,
                      Expanded(
                        flex: 2,
                        child: ElevatedButton(
                          onPressed: blocked ? null : _save,
                          child: Text(
                            _uploading
                                ? LocalKeys.uploadingFileMightTakeSomeTime
                                : (_isEditing
                                    ? LocalKeys.save
                                    : LocalKeys.add),
                          ),
                        ),
                      ),
                    ],
                  );
                },
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// The server requires `image_url`, and the app renders it with `Image.network`,
  /// so a value that is neither a URL nor a bare media path would render as a
  /// broken image.
  String? _validateImageUrl(String? value) {
    final text = (value ?? '').trim();
    if (text.isEmpty) return LocalKeys.fieldIsRequired;
    if (text.startsWith('http://') ||
        text.startsWith('https://') ||
        text.startsWith('/')) {
      return null;
    }
    return LocalKeys.invalidUrl;
  }
}
