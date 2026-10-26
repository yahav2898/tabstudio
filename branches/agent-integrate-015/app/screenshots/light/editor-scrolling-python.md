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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 1.74 | 10.40 | 99% | 0.39 | 1.48 | 3.56 | 125.3 → 132.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.67 | 1.97 | 12.02 | 99% | 0.60 | 1.58 | 3.91 | 161.8 → 159.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.72 | 2.02 | 12.75 | 99% | 0.72 | 1.50 | 4.01 | 207.9 → 193.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.62 | 1.89 | 12.36 | 99% | 0.52 | 1.51 | 3.87 | 146.8 → 148.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 2.01 | 10.47 | 99% | 0.52 | 1.43 | 3.77 | 121.7 → 120.3 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.74 | 3.04 | 77.40 | 99% | 0.70 | 2.32 | 4.07 | 147.2 → 142.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 1.98 | 13.04 | 99% | 0.54 | 1.55 | 3.94 | 148.9 → 145.7 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 2.07 | 13.05 | 99% | 0.60 | 1.63 | 4.35 | 151.5 → 148.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 1.71 | 9.19 | 99% | 0.41 | 1.42 | 3.70 | 110.9 → 109.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 1.87 | 10.47 | 99% | 0.51 | 1.49 | 3.89 | 151.0 → 148.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 4.13 | 74.96 | 99% | 0.99 | 3.38 | 3.80 | 151.9 → 148.7 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 1.85 | 8.60 | 100% | 0.47 | 1.47 | 3.43 | 73.0 → 72.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.284 |
| ArrangingLayoutable NumberDrawing | 0.074 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 2.21 | 10.25 | 99% | 0.64 | 1.72 | 4.13 | 148.2 → 148.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.67 | 4.26 | 21.50 | 96% | 0.58 | 3.53 | 4.20 | 550.0 → 565.6 | 12 | 3.5 / 31 |

