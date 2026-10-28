# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.94 | 4.06 | 13.17 | 99% | 0.54 | 3.39 | 6.67 | 134.9 → 143.2 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 3.30 | 14.51 | 99% | 0.62 | 2.82 | 5.96 | 171.7 → 169.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.02 | 3.53 | 15.03 | 99% | 0.87 | 2.81 | 6.06 | 217.7 → 202.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.90 | 3.62 | 14.82 | 99% | 0.69 | 2.98 | 6.41 | 156.9 → 158.0 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.74 | 3.33 | 12.29 | 99% | 0.62 | 2.88 | 6.55 | 131.5 → 129.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.78 | 3.34 | 16.04 | 99% | 0.61 | 2.84 | 6.11 | 156.8 → 152.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 3.33 | 18.91 | 99% | 0.60 | 2.85 | 5.96 | 157.9 → 154.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.00 | 5.30 | 17.95 | 99% | 0.78 | 3.71 | 6.48 | 162.2 → 158.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.67 | 3.09 | 12.94 | 99% | 0.45 | 2.74 | 5.65 | 120.2 → 118.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.82 | 3.28 | 14.96 | 99% | 0.60 | 2.84 | 5.98 | 161.4 → 158.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.01 | 5.23 | 17.68 | 99% | 0.83 | 3.21 | 6.10 | 160.4 → 158.1 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 2.90 | 9.39 | 99% | 0.48 | 2.56 | 4.98 | 82.0 → 81.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.312 |
| ArrangingLayoutable TextView | 0.018 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.84 | 3.43 | 15.59 | 99% | 0.62 | 2.91 | 6.10 | 156.5 → 158.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.96 | 10.89 | 89.19 | 94% | 0.85 | 10.03 | 7.74 | 575.4 → 587.6 | 13 | 3.5 / 31 |

