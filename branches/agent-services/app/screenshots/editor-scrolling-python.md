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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.22 | 13.20 | 99% | 0.45 | 1.84 | 4.43 | 125.3 → 132.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 4.16 | 17.58 | 99% | 0.86 | 2.91 | 5.17 | 162.9 → 160.4 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 2.98 | 16.59 | 99% | 0.91 | 2.36 | 4.85 | 209.5 → 194.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.25 | 14.20 | 99% | 0.63 | 1.79 | 4.67 | 147.1 → 148.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 2.32 | 14.51 | 99% | 0.65 | 1.73 | 4.79 | 121.5 → 120.0 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 2.68 | 12.84 | 99% | 0.69 | 1.82 | 4.85 | 146.9 → 142.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 2.13 | 15.30 | 99% | 0.60 | 1.74 | 4.64 | 148.9 → 145.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.75 | 2.16 | 13.76 | 99% | 0.60 | 1.70 | 4.45 | 151.6 → 148.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 2.79 | 10.74 | 99% | 0.52 | 2.45 | 5.09 | 110.8 → 109.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.20 | 12.31 | 99% | 0.63 | 1.71 | 4.70 | 151.7 → 148.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.74 | 2.16 | 14.18 | 99% | 0.60 | 1.73 | 4.21 | 151.2 → 148.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 1.74 | 9.09 | 99% | 0.45 | 1.41 | 3.80 | 71.6 → 70.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.208 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 4.15 | 12.59 | 99% | 0.73 | 3.72 | 4.97 | 148.0 → 148.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.30 | 4.88 | 23.76 | 96% | 0.75 | 4.03 | 5.03 | 556.3 → 577.8 | 12 | 3.5 / 31 |

