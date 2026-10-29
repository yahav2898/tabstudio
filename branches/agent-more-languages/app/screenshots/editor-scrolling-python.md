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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 3.12 | 12.47 | 99% | 0.40 | 2.78 | 5.96 | 134.0 → 142.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 3.52 | 14.97 | 99% | 0.68 | 2.96 | 6.36 | 171.6 → 169.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.20 | 6.07 | 86.58 | 98% | 2.90 | 3.21 | 6.87 | 219.3 → 202.6 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 3.25 | 16.38 | 99% | 0.62 | 2.81 | 6.11 | 156.9 → 158.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.67 | 3.18 | 14.30 | 99% | 0.61 | 2.65 | 5.92 | 131.0 → 129.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.04 | 5.24 | 15.25 | 99% | 1.07 | 3.07 | 6.85 | 157.0 → 153.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 3.29 | 14.66 | 99% | 0.63 | 2.81 | 6.10 | 157.8 → 155.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.91 | 3.32 | 15.67 | 99% | 0.64 | 2.84 | 6.26 | 161.1 → 158.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 3.35 | 10.72 | 99% | 0.57 | 2.87 | 5.92 | 120.9 → 119.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.95 | 3.52 | 87.84 | 99% | 0.70 | 3.01 | 6.93 | 161.6 → 158.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.02 | 3.56 | 14.90 | 99% | 0.67 | 2.97 | 5.73 | 161.4 → 157.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.58 | 3.02 | 9.22 | 100% | 0.51 | 2.61 | 5.06 | 82.3 → 82.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.325 |
| ArrangingLayoutable LineDrawing | 0.022 |
| ArrangingLayoutable TextView | 0.021 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable CachedLineNumberMargin | 0.013 |
| FindingResource - | 0.013 |
| MeasuringLayoutable Border | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.08 | 3.69 | 15.65 | 99% | 0.68 | 3.15 | 7.18 | 158.3 → 158.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.95 | 6.56 | 35.68 | 96% | 0.83 | 5.57 | 8.07 | 571.2 → 595.1 | 13 | 3.5 / 31 |

