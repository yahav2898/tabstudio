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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 2.58 | 10.41 | 99% | 0.38 | 2.34 | 4.10 | 122.1 → 129.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 3.12 | 16.60 | 99% | 0.71 | 2.51 | 4.91 | 158.5 → 156.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 3.72 | 15.06 | 99% | 0.99 | 2.35 | 4.92 | 205.3 → 190.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 2.60 | 14.05 | 99% | 0.57 | 2.22 | 4.74 | 143.8 → 145.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 1.94 | 13.70 | 99% | 0.53 | 1.60 | 4.60 | 118.7 → 117.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 4.33 | 77.23 | 99% | 0.68 | 2.66 | 5.25 | 143.6 → 140.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.68 | 2.83 | 12.73 | 99% | 0.56 | 2.40 | 4.78 | 145.8 → 142.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 2.73 | 12.14 | 99% | 0.56 | 2.32 | 4.57 | 148.3 → 145.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 2.54 | 11.69 | 99% | 0.41 | 2.22 | 4.42 | 108.1 → 106.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.79 | 3.24 | 18.34 | 99% | 0.69 | 2.53 | 4.74 | 148.1 → 145.5 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.70 | 2.54 | 11.74 | 99% | 0.57 | 2.24 | 4.36 | 148.3 → 145.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 2.20 | 12.15 | 100% | 0.39 | 2.00 | 3.85 | 69.4 → 68.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.177 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.63 | 12.18 | 99% | 0.54 | 2.29 | 4.56 | 144.2 → 145.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.01 | 8.06 | 111.33 | 95% | 0.85 | 7.67 | 5.57 | 549.9 → 570.9 | 13 | 3.5 / 31 |

