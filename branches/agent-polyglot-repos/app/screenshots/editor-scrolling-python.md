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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 1.92 | 10.74 | 99% | 0.43 | 1.51 | 3.13 | 127.3 → 135.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 2.07 | 13.15 | 99% | 0.56 | 1.55 | 3.30 | 163.9 → 161.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 2.27 | 13.27 | 99% | 0.72 | 1.67 | 3.46 | 210.9 → 196.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.71 | 2.14 | 14.60 | 99% | 0.52 | 1.61 | 3.28 | 149.2 → 150.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 3.74 | 18.14 | 99% | 0.76 | 2.41 | 3.50 | 124.7 → 123.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.72 | 1.96 | 12.33 | 99% | 0.58 | 1.51 | 3.20 | 148.7 → 144.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 2.22 | 14.01 | 99% | 0.61 | 1.67 | 3.32 | 150.7 → 148.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.74 | 2.22 | 14.73 | 99% | 0.60 | 1.67 | 3.50 | 154.1 → 150.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.62 | 1.89 | 10.33 | 99% | 0.52 | 1.53 | 3.51 | 113.6 → 111.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.77 | 2.21 | 12.73 | 99% | 0.57 | 1.65 | 3.45 | 153.8 → 150.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.86 | 4.06 | 14.28 | 99% | 0.81 | 2.64 | 3.17 | 153.9 → 150.9 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.71 | 5.69 | 100% | 0.37 | 1.28 | 2.43 | 74.6 → 73.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.137 |
| ArrangingLayoutable NumberDrawing | 0.040 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.10 | 12.83 | 99% | 0.57 | 1.60 | 3.27 | 149.3 → 150.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.29 | 3.34 | 21.54 | 96% | 0.46 | 2.91 | 3.29 | 555.0 → 573.9 | 13 | 3.5 / 31 |

