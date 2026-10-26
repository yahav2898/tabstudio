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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.35 | 1.22 | 11.52 | 100% | 0.25 | 1.07 | 2.52 | 121.2 → 128.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 2.88 | 14.47 | 99% | 0.65 | 2.37 | 3.10 | 158.4 → 156.6 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.48 | 1.59 | 10.77 | 99% | 0.58 | 1.10 | 2.97 | 205.5 → 190.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.39 | 1.29 | 10.55 | 99% | 0.36 | 1.07 | 2.78 | 143.0 → 144.2 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.31 | 1.17 | 7.95 | 100% | 0.35 | 0.96 | 2.66 | 117.4 → 115.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.36 | 1.21 | 10.04 | 99% | 0.37 | 1.01 | 2.76 | 142.5 → 138.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.38 | 1.30 | 10.78 | 99% | 0.35 | 1.03 | 2.80 | 144.9 → 141.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.39 | 1.36 | 9.47 | 99% | 0.38 | 1.08 | 2.81 | 147.5 → 144.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.44 | 3.16 | 10.72 | 99% | 0.38 | 2.32 | 2.76 | 107.2 → 105.3 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.45 | 1.58 | 9.25 | 100% | 0.44 | 1.20 | 3.83 | 147.7 → 144.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.38 | 1.28 | 11.46 | 99% | 0.38 | 1.07 | 2.48 | 147.4 → 144.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.22 | 1.20 | 8.20 | 100% | 0.35 | 0.93 | 3.06 | 67.9 → 67.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.127 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| FindingResource - | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.39 | 1.33 | 12.55 | 99% | 0.37 | 1.08 | 2.92 | 143.3 → 144.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.34 | 4.91 | 24.66 | 96% | 0.57 | 4.35 | 3.21 | 547.2 → 575.2 | 14 | 3.5 / 31 |

