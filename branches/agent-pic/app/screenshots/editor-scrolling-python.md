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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 1.57 | 10.33 | 99% | 0.36 | 1.32 | 2.83 | 120.9 → 127.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.69 | 3.44 | 13.92 | 99% | 0.60 | 2.38 | 4.06 | 157.6 → 155.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 3.58 | 12.46 | 99% | 0.98 | 2.54 | 3.57 | 204.3 → 189.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 2.16 | 13.22 | 99% | 0.57 | 1.60 | 3.58 | 142.6 → 144.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 1.76 | 12.59 | 99% | 0.56 | 1.38 | 3.04 | 117.4 → 115.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.45 | 1.58 | 13.56 | 99% | 0.43 | 1.25 | 2.85 | 141.6 → 137.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.51 | 1.52 | 12.40 | 99% | 0.44 | 1.26 | 2.89 | 143.9 → 140.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.51 | 1.68 | 12.10 | 99% | 0.48 | 1.29 | 2.99 | 146.8 → 143.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.70 | 17.78 | 99% | 0.45 | 2.38 | 2.95 | 107.0 → 105.0 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.86 | 2.98 | 12.31 | 99% | 0.77 | 1.81 | 4.18 | 147.5 → 144.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 1.83 | 15.89 | 99% | 0.50 | 1.45 | 2.75 | 146.9 → 143.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.46 | 12.91 | 100% | 0.40 | 1.17 | 2.36 | 68.0 → 67.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.219 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 1.96 | 10.45 | 99% | 0.54 | 1.52 | 3.22 | 143.1 → 143.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.75 | 9.71 | 78.52 | 95% | 2.41 | 6.45 | 4.97 | 548.7 → 571.3 | 13 | 3.5 / 31 |

