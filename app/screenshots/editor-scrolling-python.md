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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 1.55 | 9.60 | 99% | 0.36 | 1.31 | 2.82 | 121.7 → 129.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.93 | 2.36 | 13.21 | 99% | 0.67 | 1.78 | 3.64 | 159.0 → 157.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.82 | 2.55 | 15.82 | 99% | 0.85 | 1.84 | 3.73 | 206.3 → 191.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.17 | 14.80 | 99% | 0.55 | 1.54 | 3.25 | 143.8 → 145.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.85 | 3.48 | 14.85 | 99% | 0.77 | 2.51 | 3.58 | 119.0 → 117.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.78 | 2.05 | 14.43 | 99% | 0.57 | 1.62 | 3.37 | 143.7 → 139.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.67 | 1.98 | 14.67 | 99% | 0.58 | 1.53 | 3.34 | 145.6 → 142.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.92 | 2.42 | 13.54 | 99% | 0.68 | 1.78 | 3.54 | 148.9 → 145.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.78 | 2.26 | 9.62 | 100% | 0.52 | 1.74 | 3.34 | 107.9 → 105.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 2.25 | 11.81 | 99% | 0.59 | 1.71 | 3.39 | 148.8 → 145.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.87 | 4.11 | 80.16 | 99% | 0.74 | 2.64 | 3.18 | 148.4 → 145.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 1.81 | 6.50 | 100% | 0.49 | 1.37 | 2.81 | 68.5 → 68.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.184 |
| AttachingStyle - | 0.066 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable Grid | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 1.85 | 12.83 | 99% | 0.52 | 1.43 | 3.97 | 144.1 → 145.2 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.96 | 4.64 | 52.13 | 97% | 0.72 | 3.84 | 4.17 | 552.0 → 573.7 | 13 | 3.5 / 31 |

