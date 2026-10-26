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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 1.84 | 12.96 | 99% | 0.38 | 1.52 | 3.65 | 128.7 → 135.3 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.74 | 2.09 | 14.24 | 99% | 0.57 | 1.59 | 4.15 | 164.6 → 162.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.81 | 2.25 | 14.64 | 99% | 0.73 | 1.64 | 4.29 | 211.4 → 196.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.03 | 18.78 | 99% | 0.66 | 2.01 | 4.25 | 150.0 → 151.6 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.63 | 2.83 | 10.33 | 99% | 0.62 | 1.62 | 4.20 | 125.2 → 122.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.66 | 2.10 | 16.18 | 99% | 0.57 | 1.65 | 4.09 | 149.5 → 145.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.15 | 13.94 | 99% | 0.57 | 1.73 | 4.38 | 151.6 → 148.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 2.08 | 13.53 | 99% | 0.55 | 1.67 | 4.22 | 154.8 → 151.2 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.57 | 1.88 | 9.51 | 99% | 0.42 | 1.56 | 3.95 | 113.6 → 112.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 4.02 | 13.14 | 99% | 2.31 | 2.58 | 4.37 | 154.7 → 152.2 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 1.97 | 13.00 | 99% | 0.54 | 1.63 | 3.70 | 153.9 → 150.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.69 | 12.14 | 99% | 0.44 | 1.35 | 3.36 | 75.4 → 74.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.235 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.011 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.71 | 2.04 | 15.83 | 99% | 0.54 | 1.66 | 4.06 | 150.5 → 151.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.07 | 6.37 | 30.09 | 96% | 0.75 | 5.18 | 4.76 | 556.5 → 583.7 | 14 | 3.5 / 31 |

