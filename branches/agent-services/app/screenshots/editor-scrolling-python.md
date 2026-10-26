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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.84 | 2.91 | 12.33 | 99% | 0.54 | 2.41 | 4.27 | 125.1 → 132.8 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.13 | 3.57 | 14.39 | 99% | 0.81 | 2.61 | 4.68 | 162.4 → 160.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.15 | 3.64 | 80.40 | 99% | 1.72 | 2.54 | 4.70 | 210.1 → 194.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.97 | 2.66 | 16.29 | 99% | 0.70 | 2.07 | 4.40 | 147.1 → 148.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.77 | 2.17 | 12.19 | 99% | 0.64 | 1.65 | 4.14 | 121.7 → 119.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 2.78 | 18.64 | 99% | 0.73 | 2.17 | 4.44 | 146.9 → 142.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.97 | 4.60 | 14.41 | 99% | 0.76 | 3.85 | 4.74 | 149.2 → 145.9 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.83 | 2.35 | 14.61 | 99% | 0.64 | 1.91 | 4.06 | 151.6 → 148.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.74 | 2.48 | 10.30 | 99% | 0.50 | 2.08 | 3.94 | 110.8 → 108.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.95 | 2.65 | 15.10 | 99% | 0.71 | 2.16 | 4.40 | 151.4 → 148.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.92 | 3.18 | 14.94 | 99% | 0.74 | 2.46 | 4.16 | 151.4 → 148.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.63 | 2.66 | 13.48 | 100% | 0.58 | 1.99 | 3.42 | 72.4 → 70.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.234 |
| AttachingStyle - | 0.051 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.06 | 2.88 | 19.34 | 99% | 0.75 | 2.30 | 4.62 | 147.6 → 148.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.10 | 5.48 | 24.91 | 96% | 0.70 | 4.66 | 4.66 | 561.1 → 575.5 | 13 | 3.5 / 31 |

