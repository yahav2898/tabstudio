# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 779 × 614 dip.
- 0 classified spans, 5 diagnostics, 683 folds, 6 breakpoints, 0 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, TextMateColoringTransformer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator, InlayHintGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.36 | 2.07 | 11.71 | 100% | 0.30 | 1.82 | 2.55 | 96.4 → 103.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.48 | 2.22 | 22.08 | 99% | 1.03 | 1.88 | 3.10 | 133.1 → 130.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.64 | 2.56 | 15.96 | 99% | 1.31 | 2.04 | 3.43 | 180.7 → 165.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.47 | 2.31 | 13.73 | 99% | 0.51 | 1.94 | 3.02 | 118.1 → 119.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 2.45 | 14.37 | 99% | 1.19 | 1.94 | 2.98 | 93.0 → 91.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.87 | 3.16 | 15.60 | 99% | 1.69 | 2.30 | 3.80 | 118.0 → 113.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.59 | 2.58 | 14.75 | 99% | 0.61 | 2.05 | 3.29 | 120.1 → 116.5 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.60 | 2.39 | 14.93 | 99% | 0.81 | 2.00 | 3.13 | 123.1 → 119.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.52 | 2.25 | 11.90 | 99% | 0.43 | 1.88 | 2.81 | 82.2 → 80.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 2.62 | 16.11 | 99% | 1.14 | 2.11 | 3.63 | 122.8 → 119.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 2.61 | 15.76 | 99% | 1.13 | 2.11 | 2.87 | 122.9 → 119.3 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.53 | 73.79 | 99% | 1.32 | 1.94 | 2.87 | 43.8 → 43.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.242 |
| ArrangingLayoutable Track | 0.017 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.56 | 2.46 | 19.74 | 99% | 0.50 | 2.04 | 3.09 | 118.7 → 119.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.66 | 4.82 | 23.98 | 96% | 0.58 | 4.28 | 5.64 | 593.1 → 613.5 | 13 | 3.5 / 31 |

