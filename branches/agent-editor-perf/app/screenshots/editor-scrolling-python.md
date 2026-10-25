# Scrolling a long Python file

- 2211 lines; 29 visible lines; viewport 827 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.48 | 1.68 | 13.12 | 100% | 0.36 | 1.43 | 4.01 | 88.4 → 95.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.16 | 61.20 | 99% | 0.80 | 2.31 | 4.69 | 124.8 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.75 | 2.09 | 12.17 | 99% | 0.82 | 1.46 | 4.46 | 172.0 → 157.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.56 | 1.84 | 13.32 | 99% | 0.55 | 1.44 | 4.34 | 109.9 → 111.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.52 | 2.79 | 13.15 | 99% | 0.67 | 1.65 | 4.41 | 84.8 → 82.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 1.78 | 14.52 | 99% | 0.56 | 1.38 | 4.32 | 109.7 → 105.7 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.58 | 1.86 | 12.86 | 99% | 0.54 | 1.45 | 4.55 | 111.9 → 108.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.08 | 60.26 | 99% | 0.83 | 1.76 | 4.82 | 114.8 → 111.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.64 | 10.51 | 99% | 0.40 | 1.32 | 4.04 | 73.9 → 71.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.57 | 1.81 | 13.32 | 99% | 0.53 | 1.40 | 4.18 | 114.5 → 111.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.56 | 2.11 | 14.58 | 99% | 0.61 | 1.51 | 3.78 | 114.7 → 111.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 1.53 | 6.27 | 100% | 0.40 | 1.20 | 3.53 | 35.5 → 34.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.175 |
| ArrangingLayoutable NumberDrawing | 0.045 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 1.79 | 12.05 | 99% | 0.53 | 1.43 | 4.19 | 110.5 → 111.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.52 | 8.32 | 25.87 | 95% | 0.82 | 7.97 | 8.56 | 601.0 → 603.5 | 15 | 3.5 / 31 |

