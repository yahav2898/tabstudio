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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.39 | 1.35 | 8.49 | 100% | 0.30 | 1.15 | 2.63 | 126.3 → 133.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.49 | 1.44 | 11.24 | 99% | 0.40 | 1.17 | 2.80 | 162.8 → 160.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.54 | 1.56 | 12.16 | 99% | 0.53 | 1.19 | 2.90 | 209.4 → 194.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.42 | 1.36 | 10.73 | 99% | 0.38 | 1.13 | 2.72 | 147.9 → 149.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 3.48 | 27.42 | 99% | 0.77 | 2.54 | 5.51 | 124.3 → 121.6 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.49 | 3.28 | 12.17 | 99% | 0.50 | 2.35 | 3.00 | 147.9 → 144.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.38 | 1.29 | 11.78 | 100% | 0.37 | 1.09 | 2.73 | 149.4 → 146.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.38 | 1.36 | 10.77 | 99% | 0.34 | 1.11 | 2.67 | 152.3 → 149.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.33 | 1.64 | 7.34 | 100% | 0.26 | 1.48 | 2.57 | 112.1 → 110.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.39 | 1.42 | 10.78 | 100% | 0.38 | 1.11 | 2.91 | 152.0 → 149.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.37 | 1.32 | 10.67 | 99% | 0.37 | 1.08 | 2.39 | 151.8 → 148.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.21 | 1.05 | 7.71 | 100% | 0.23 | 0.92 | 2.12 | 73.3 → 72.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.196 |
| ArrangingLayoutable CachedLineNumberMargin | 0.027 |
| AttachingStyle - | 0.020 |
| ArrangingLayoutable TextView | 0.019 |
| ArrangingLayoutable LineDrawing | 0.016 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.52 | 3.08 | 16.71 | 99% | 0.53 | 2.39 | 3.06 | 148.8 → 150.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.16 | 3.20 | 17.81 | 96% | 0.40 | 2.74 | 3.07 | 548.5 → 568.8 | 12 | 3.5 / 31 |

