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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 1.49 | 9.19 | 100% | 0.32 | 1.24 | 3.25 | 122.0 → 129.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.59 | 1.66 | 12.01 | 99% | 0.49 | 1.28 | 3.60 | 158.3 → 156.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 2.21 | 13.12 | 99% | 0.69 | 1.47 | 3.83 | 205.4 → 190.7 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 3.15 | 67.60 | 99% | 0.72 | 2.45 | 3.98 | 144.3 → 144.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 1.52 | 12.22 | 100% | 0.46 | 1.18 | 3.58 | 118.0 → 116.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.53 | 1.54 | 12.98 | 99% | 0.48 | 1.22 | 3.54 | 142.9 → 139.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 1.74 | 12.21 | 99% | 0.53 | 1.34 | 3.70 | 145.3 → 141.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.56 | 1.79 | 10.99 | 99% | 0.50 | 1.32 | 3.68 | 147.9 → 145.2 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 2.80 | 10.81 | 100% | 0.46 | 2.39 | 3.99 | 107.3 → 105.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.53 | 1.59 | 16.84 | 99% | 0.47 | 1.28 | 3.56 | 147.6 → 144.7 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.51 | 1.57 | 10.78 | 99% | 0.47 | 1.26 | 3.27 | 147.6 → 144.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.30 | 1.21 | 7.78 | 100% | 0.32 | 1.01 | 3.02 | 68.0 → 67.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.147 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.59 | 2.69 | 13.17 | 99% | 0.55 | 1.45 | 3.63 | 143.8 → 145.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.55 | 4.83 | 21.19 | 96% | 0.56 | 4.45 | 3.83 | 542.5 → 560.9 | 13 | 3.5 / 31 |

