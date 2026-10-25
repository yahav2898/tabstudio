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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 1.74 | 11.79 | 99% | 0.36 | 1.51 | 3.76 | 120.3 → 127.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 4.03 | 65.32 | 99% | 0.70 | 2.52 | 4.58 | 156.9 → 155.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 2.28 | 14.10 | 99% | 0.82 | 1.59 | 4.54 | 203.7 → 188.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 1.94 | 13.83 | 99% | 0.54 | 1.55 | 4.32 | 142.1 → 143.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 1.70 | 10.49 | 99% | 0.52 | 1.38 | 4.30 | 116.1 → 114.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 3.96 | 63.77 | 99% | 0.71 | 2.54 | 4.44 | 141.6 → 138.0 | 5 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.63 | 1.95 | 13.33 | 99% | 0.54 | 1.55 | 4.58 | 143.7 → 140.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.62 | 1.92 | 13.47 | 99% | 0.55 | 1.56 | 4.30 | 146.6 → 143.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 1.75 | 9.57 | 99% | 0.41 | 1.46 | 3.80 | 105.2 → 103.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.75 | 3.36 | 66.64 | 99% | 0.71 | 2.53 | 4.58 | 146.8 → 143.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.60 | 1.92 | 14.75 | 99% | 0.55 | 1.51 | 3.94 | 146.0 → 142.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.52 | 10.45 | 100% | 0.39 | 1.25 | 3.66 | 66.6 → 66.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.175 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.97 | 17.57 | 99% | 2.41 | 2.54 | 4.75 | 142.4 → 143.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.96 | 4.36 | 22.04 | 96% | 0.67 | 3.79 | 4.77 | 552.8 → 563.9 | 13 | 3.5 / 31 |

