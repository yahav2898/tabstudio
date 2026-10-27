# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.24 | 3.31 | 14.94 | 99% | 0.67 | 2.57 | 7.66 | 135.4 → 143.1 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.46 | 3.52 | 16.56 | 99% | 0.94 | 2.60 | 7.73 | 172.0 → 169.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.65 | 3.61 | 17.06 | 99% | 1.18 | 2.53 | 7.80 | 217.7 → 202.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.34 | 3.12 | 16.07 | 99% | 0.86 | 2.33 | 7.54 | 157.1 → 158.0 | 5 | 1.1 / 4 |
| without line numbers | 368 | 1.19 | 4.69 | 92.52 | 99% | 0.91 | 4.00 | 7.58 | 131.3 → 130.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.32 | 3.15 | 17.09 | 99% | 0.90 | 2.41 | 7.70 | 156.0 → 152.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.41 | 3.32 | 16.99 | 99% | 0.91 | 2.53 | 7.87 | 158.4 → 155.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.37 | 4.70 | 15.12 | 99% | 1.00 | 3.02 | 8.08 | 161.5 → 158.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.04 | 2.78 | 12.06 | 99% | 0.64 | 2.20 | 7.05 | 120.2 → 119.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.30 | 3.10 | 16.20 | 99% | 0.83 | 2.41 | 7.66 | 160.5 → 157.6 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.32 | 3.16 | 15.46 | 99% | 0.84 | 2.37 | 6.92 | 160.4 → 158.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.84 | 2.87 | 13.59 | 99% | 0.68 | 2.03 | 6.25 | 82.1 → 81.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.296 |
| AttachingStyle - | 0.057 |
| ArrangingLayoutable TextView | 0.024 |
| FindingResource - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.25 | 3.15 | 17.62 | 99% | 0.82 | 2.40 | 7.48 | 157.3 → 158.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 4.48 | 10.41 | 97.02 | 93% | 1.27 | 7.57 | 9.11 | 567.7 → 578.7 | 14 | 3.5 / 31 |

