# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.40 | 1.80 | 10.91 | 99% | 0.30 | 1.62 | 2.68 | 120.7 → 128.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.54 | 2.02 | 12.86 | 99% | 0.47 | 1.72 | 3.07 | 157.3 → 154.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.95 | 3.70 | 16.53 | 99% | 1.33 | 2.55 | 3.55 | 204.4 → 190.0 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 2.69 | 11.67 | 99% | 0.58 | 1.91 | 3.29 | 143.2 → 144.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 2.05 | 13.88 | 99% | 0.52 | 1.64 | 3.12 | 117.6 → 115.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 2.02 | 14.63 | 99% | 0.49 | 1.66 | 3.08 | 142.1 → 138.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.54 | 2.05 | 14.90 | 99% | 0.48 | 1.76 | 3.22 | 144.5 → 141.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.51 | 1.96 | 12.79 | 99% | 0.47 | 1.65 | 3.02 | 147.1 → 143.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 2.91 | 11.19 | 99% | 0.46 | 2.37 | 3.21 | 106.9 → 105.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 2.45 | 14.24 | 99% | 0.59 | 1.94 | 3.49 | 147.6 → 144.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 2.27 | 15.07 | 99% | 0.58 | 1.84 | 3.01 | 147.0 → 143.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 1.83 | 9.24 | 100% | 0.39 | 1.54 | 2.71 | 68.7 → 67.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.286 |
| ArrangingLayoutable TextView | 0.013 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.82 | 2.52 | 16.91 | 99% | 0.65 | 2.14 | 3.57 | 143.7 → 144.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.60 | 8.06 | 77.44 | 95% | 2.33 | 7.18 | 5.21 | 553.7 → 572.7 | 13 | 3.5 / 31 |

