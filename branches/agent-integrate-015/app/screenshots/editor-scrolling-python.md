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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.87 | 2.40 | 16.18 | 100% | 0.58 | 1.89 | 4.28 | 127.1 → 133.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.95 | 2.42 | 15.50 | 99% | 0.69 | 1.88 | 4.26 | 162.9 → 160.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 2.65 | 15.79 | 99% | 0.84 | 1.86 | 4.46 | 209.5 → 195.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 2.61 | 14.84 | 99% | 0.57 | 2.12 | 4.20 | 148.1 → 149.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 3.77 | 84.61 | 99% | 0.65 | 2.48 | 4.36 | 123.2 → 121.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.86 | 3.05 | 15.67 | 99% | 0.76 | 2.44 | 4.80 | 147.8 → 144.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.75 | 17.99 | 99% | 0.66 | 2.29 | 4.35 | 150.2 → 146.7 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.86 | 2.82 | 15.64 | 99% | 0.66 | 2.41 | 4.22 | 152.7 → 149.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 2.71 | 12.28 | 99% | 0.52 | 2.33 | 3.98 | 112.4 → 111.0 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.97 | 4.34 | 18.73 | 99% | 2.27 | 3.79 | 4.76 | 152.9 → 150.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 2.77 | 37.27 | 99% | 0.61 | 2.35 | 3.94 | 152.5 → 149.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.29 | 9.88 | 100% | 0.40 | 2.00 | 3.25 | 73.6 → 72.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.343 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 2.81 | 16.29 | 99% | 0.59 | 2.40 | 4.30 | 148.7 → 149.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 3.13 | 8.01 | 89.90 | 95% | 2.47 | 7.27 | 5.76 | 558.9 → 575.8 | 14 | 3.5 / 31 |

