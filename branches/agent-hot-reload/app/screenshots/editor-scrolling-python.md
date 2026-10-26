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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.74 | 2.36 | 12.34 | 99% | 0.47 | 1.95 | 4.52 | 122.9 → 130.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.96 | 3.58 | 16.93 | 99% | 0.80 | 2.66 | 5.16 | 160.8 → 157.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 2.83 | 14.86 | 99% | 0.84 | 2.29 | 4.79 | 207.1 → 192.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.77 | 2.68 | 14.30 | 99% | 0.61 | 2.24 | 4.72 | 145.0 → 146.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.77 | 13.66 | 99% | 0.67 | 1.77 | 4.74 | 119.4 → 117.9 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 2.54 | 98.28 | 99% | 0.65 | 1.84 | 4.96 | 144.8 → 140.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 2.12 | 14.25 | 99% | 0.61 | 1.64 | 4.84 | 146.5 → 143.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 2.08 | 13.46 | 99% | 0.60 | 1.65 | 4.70 | 149.4 → 146.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 2.39 | 9.92 | 99% | 0.52 | 1.74 | 4.55 | 108.6 → 106.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.51 | 67.94 | 99% | 0.63 | 1.84 | 4.86 | 149.6 → 146.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.14 | 13.46 | 99% | 0.65 | 1.73 | 4.38 | 149.1 → 145.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 1.69 | 10.14 | 99% | 0.41 | 1.39 | 3.84 | 69.2 → 68.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.266 |
| AttachingStyle - | 0.025 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| ArrangingLayoutable TextView | 0.015 |
| ArrangingLayoutable RepeatButton | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.83 | 3.71 | 72.74 | 99% | 0.84 | 2.39 | 5.31 | 145.9 → 146.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.12 | 4.53 | 23.93 | 96% | 0.71 | 4.02 | 4.95 | 554.6 → 571.8 | 13 | 3.5 / 31 |

