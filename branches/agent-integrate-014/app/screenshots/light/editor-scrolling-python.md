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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 1.97 | 11.66 | 99% | 0.41 | 1.70 | 4.10 | 121.9 → 129.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.11 | 17.01 | 99% | 0.59 | 1.67 | 4.76 | 158.0 → 155.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 2.38 | 15.86 | 99% | 0.85 | 1.72 | 4.90 | 204.4 → 189.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 2.09 | 14.27 | 99% | 0.56 | 1.68 | 4.64 | 143.3 → 144.8 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.87 | 15.36 | 99% | 0.71 | 2.36 | 4.88 | 118.4 → 117.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 2.02 | 15.50 | 99% | 0.55 | 1.62 | 4.75 | 143.0 → 139.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 2.02 | 14.72 | 99% | 0.55 | 1.64 | 4.74 | 145.0 → 141.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.05 | 13.94 | 99% | 0.55 | 1.61 | 4.44 | 147.7 → 144.8 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 2.75 | 11.36 | 99% | 0.49 | 1.77 | 4.57 | 107.4 → 106.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.58 | 78.26 | 99% | 0.60 | 1.81 | 5.20 | 147.8 → 144.9 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.63 | 2.02 | 15.38 | 99% | 0.57 | 1.65 | 4.05 | 147.3 → 144.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 1.56 | 9.72 | 100% | 0.38 | 1.33 | 3.85 | 68.7 → 68.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.228 |
| AttachingStyle - | 0.056 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.97 | 16.87 | 99% | 0.62 | 2.26 | 4.77 | 143.7 → 145.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.09 | 4.53 | 32.43 | 96% | 0.69 | 3.95 | 4.90 | 543.9 → 563.1 | 13 | 3.5 / 31 |

