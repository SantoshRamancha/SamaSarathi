import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/driver_models.dart';
import '../../../providers.dart';
import 'step_save_mixin.dart';

class DocumentsStep extends ConsumerStatefulWidget {
  const DocumentsStep({super.key, required this.me, required this.onSaved});

  final DriverMe? me;
  final OnStepSaved onSaved;

  @override
  ConsumerState<DocumentsStep> createState() => _DocumentsStepState();
}

class _DocumentsStepState extends ConsumerState<DocumentsStep> with StepSaveMixin {
  static const _maxBytes = 5 * 1024 * 1024;
  DocumentType? _uploading;

  Future<void> _pickAndUpload(DocumentType type) async {
    final file = await FilePicker.pickFile(
      type: FileType.custom,
      allowedExtensions: ['jpg', 'jpeg', 'png', 'pdf'],
    );
    if (file == null) return;
    if ((await file.length() ?? 0) > _maxBytes) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(const SnackBar(content: Text('File is larger than 5 MB. Choose a smaller one.')));
      }
      return;
    }

    setState(() => _uploading = type);
    await save(() async {
      // Reading bytes (not a file path) works the same on web and phones.
      final bytes = await file.readAsBytes();
      final me = await ref.read(driverApiProvider).uploadDocument(type, file.name, bytes);
      // Move to the next step only once every document is in.
      widget.onSaved(me, advance: me.isDone(OnboardingStep.documents));
    });
    if (mounted) setState(() => _uploading = null);
  }

  @override
  Widget build(BuildContext context) {
    final me = widget.me;
    final colors = Theme.of(context).colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('JPG, PNG or PDF, up to 5 MB each.'),
        const SizedBox(height: 8),
        for (final type in DocumentType.values)
          Card(
            child: ListTile(
              leading: Icon(
                me?.document(type) != null ? Icons.check_circle : Icons.upload_file,
                color: me?.document(type) != null ? colors.primary : colors.outline,
              ),
              title: Text(type.label),
              subtitle: Text(me?.document(type)?.fileName ?? type.hint),
              trailing: _uploading == type
                  ? const SizedBox.square(dimension: 24, child: CircularProgressIndicator(strokeWidth: 2))
                  : TextButton(
                      onPressed: me == null || saving ? null : () => _pickAndUpload(type),
                      child: Text(me?.document(type) != null ? 'Replace' : 'Upload'),
                    ),
            ),
          ),
      ],
    );
  }
}
