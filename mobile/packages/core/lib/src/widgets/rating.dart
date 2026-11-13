import 'package:flutter/material.dart';

/// Read-only stars, e.g. "★★★★☆". [value] is 0 to 5 and may be fractional (rounded to the nearest whole star).
class StarRating extends StatelessWidget {
  const StarRating({super.key, required this.value, this.size = 18});

  final double value;
  final double size;

  @override
  Widget build(BuildContext context) {
    final filled = value.round().clamp(0, 5);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (var i = 1; i <= 5; i++)
          Icon(i <= filled ? Icons.star : Icons.star_border, size: size, color: Colors.amber.shade700),
      ],
    );
  }
}

/// "★ 4.8 (23)" for someone who has been rated, "New" for someone who hasn't.
class RatingBadge extends StatelessWidget {
  const RatingBadge({super.key, required this.average, required this.count});

  final double? average;
  final int count;

  @override
  Widget build(BuildContext context) {
    final style = Theme.of(context).textTheme.bodySmall;
    if (average == null || count == 0) return Text('New', style: style);
    return Row(mainAxisSize: MainAxisSize.min, children: [
      Icon(Icons.star, size: 14, color: Colors.amber.shade700),
      const SizedBox(width: 2),
      Text('${average!.toStringAsFixed(1)} ($count)', style: style),
    ]);
  }
}

/// What a rider sees about a driver's reputation, with a plain explanation one tap away.
///
/// - Nobody has rated them yet: "☆ New driver · Verified". Every driver passes our background checks before taking
///   rides, so "new" is explained as a trust signal rather than left as a bare word.
/// - A few ratings (fewer than [minRatingsForAverage]): "★ 4.5 · 2 ratings". One 5-star rating must not look like
///   a long track record, so the count is spelled out.
/// - Enough ratings: "★ 4.8 (23)".
class DriverTrustBadge extends StatelessWidget {
  const DriverTrustBadge({
    super.key,
    required this.average,
    required this.count,
  });

  /// Ratings needed before the average is shown without a "few ratings" caution.
  static const minRatingsForAverage = 5;

  final double? average;
  final int count;

  bool get _unrated => average == null || count == 0;

  /// The text on the chip.
  String get label => _unrated
      ? 'New driver'
      : count < minRatingsForAverage
      ? '${average!.toStringAsFixed(1)} · $count rating${count == 1 ? '' : 's'}'
      : '${average!.toStringAsFixed(1)} ($count)';

  /// What tapping the chip explains.
  String get explanation => _unrated
      ? 'New to Samasarathi and not rated yet. Every driver passes our background checks before taking rides.'
      : count < minRatingsForAverage
      ? 'Based on only $count rating${count == 1 ? '' : 's'} so far, so this score may change as more riders rate.'
      : 'Average of $count ratings from riders after their trips.';

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final style = Theme.of(context).textTheme.bodySmall;
    return Semantics(
      button: true,
      label: _unrated
          ? 'New driver, verified. Tap for details.'
          : 'Rated $label. Tap for details.',
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () => showDialog<void>(
          context: context,
          builder: (context) => AlertDialog(
            title: Text(_unrated ? 'New driver' : 'Driver rating'),
            content: Text(explanation),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('OK'),
              ),
            ],
          ),
        ),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
          decoration: BoxDecoration(
            border: Border.all(color: colors.outlineVariant),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                _unrated ? Icons.star_border : Icons.star,
                size: 14,
                color: Colors.amber.shade700,
              ),
              const SizedBox(width: 2),
              Text(label, style: style),
              if (_unrated) ...[
                Text(' · ', style: style),
                Icon(Icons.verified_user, size: 14, color: colors.primary),
                const SizedBox(width: 2),
                Text('Verified', style: style),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

/// What the person typed and tapped in a [RatingForm].
class RatingResult {
  const RatingResult(this.stars, this.comment);

  final int stars;
  final String? comment;
}

/// Five tappable stars, an optional comment box and a Submit button. Used for rating a driver or a rider after a trip.
/// [onSubmit] may throw; the message is shown under the form so the person can try again.
class RatingForm extends StatefulWidget {
  const RatingForm({super.key, required this.title, required this.onSubmit, this.hint = 'Add a comment (optional)'});

  final String title;
  final String hint;
  final Future<void> Function(RatingResult result) onSubmit;

  @override
  State<RatingForm> createState() => _RatingFormState();
}

class _RatingFormState extends State<RatingForm> {
  final _comment = TextEditingController();
  int _stars = 0;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _comment.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final text = _comment.text.trim();
      await widget.onSubmit(RatingResult(_stars, text.isEmpty ? null : text));
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text(widget.title, style: text.titleSmall),
      const SizedBox(height: 4),
      Row(children: [
        for (var i = 1; i <= 5; i++)
          IconButton(
            tooltip: '$i star${i == 1 ? '' : 's'}',
            visualDensity: VisualDensity.compact,
            onPressed: _busy ? null : () => setState(() => _stars = i),
            icon: Icon(i <= _stars ? Icons.star : Icons.star_border, size: 32, color: Colors.amber.shade700),
          ),
      ]),
      TextField(
        controller: _comment,
        maxLength: 300,
        maxLines: 2,
        enabled: !_busy,
        decoration: InputDecoration(hintText: widget.hint, border: const OutlineInputBorder(), isDense: true),
      ),
      if (_error != null) Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
      const SizedBox(height: 4),
      FilledButton(
        onPressed: _busy || _stars == 0 ? null : _submit,
        child: Text(_busy ? 'Saving…' : _stars == 0 ? 'Tap a star to rate' : 'Submit rating'),
      ),
    ]);
  }
}

/// Shows [RatingForm] in a dialog. Returns true if the person submitted a rating, false/null if they closed it.
Future<bool?> showRatingDialog(
  BuildContext context, {
  required String title,
  required Future<void> Function(RatingResult result) onSubmit,
}) =>
    showDialog<bool>(
      context: context,
      builder: (context) => Dialog(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: SingleChildScrollView(
            child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              RatingForm(
                title: title,
                onSubmit: (result) async {
                  await onSubmit(result);
                  if (context.mounted) Navigator.of(context).pop(true);
                },
              ),
              TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Not now')),
            ]),
          ),
        ),
      ),
    );
