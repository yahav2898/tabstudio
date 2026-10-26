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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.99 | 3.01 | 14.25 | 99% | 0.59 | 2.42 | 4.59 | 122.8 → 130.5 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.07 | 3.22 | 14.97 | 99% | 0.75 | 2.54 | 4.56 | 159.4 → 157.4 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.14 | 4.73 | 81.34 | 99% | 0.98 | 2.71 | 4.81 | 207.3 → 191.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 2.82 | 13.41 | 99% | 0.70 | 2.27 | 4.65 | 144.5 → 145.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 2.22 | 10.98 | 99% | 0.67 | 1.65 | 4.32 | 119.1 → 117.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.96 | 2.45 | 14.52 | 99% | 0.71 | 1.82 | 4.79 | 143.9 → 140.4 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.96 | 3.07 | 14.49 | 99% | 0.85 | 1.90 | 4.50 | 146.4 → 143.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.95 | 2.38 | 14.09 | 99% | 0.68 | 1.84 | 4.44 | 149.0 → 145.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 1.97 | 10.48 | 99% | 0.53 | 1.47 | 3.98 | 107.7 → 105.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.88 | 2.20 | 13.86 | 99% | 0.66 | 1.66 | 4.50 | 148.7 → 145.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.94 | 3.14 | 18.18 | 99% | 0.75 | 2.34 | 4.26 | 148.5 → 146.0 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.69 | 2.34 | 6.93 | 100% | 0.61 | 1.54 | 3.82 | 69.3 → 68.8 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.264 |
| ArrangingLayoutable TextView | 0.024 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.93 | 2.30 | 16.20 | 100% | 0.68 | 1.71 | 4.56 | 144.9 → 145.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.07 | 6.14 | 25.13 | 96% | 0.73 | 5.43 | 6.83 | 551.1 → 574.0 | 14 | 3.5 / 31 |

