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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.36 | 1.27 | 7.45 | 100% | 0.28 | 1.09 | 2.53 | 120.1 → 127.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.42 | 1.35 | 11.53 | 99% | 0.39 | 1.09 | 2.83 | 156.3 → 153.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.55 | 1.77 | 13.14 | 99% | 0.60 | 1.26 | 3.03 | 202.9 → 188.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.54 | 3.03 | 11.36 | 99% | 0.62 | 2.33 | 3.24 | 142.1 → 143.7 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.38 | 2.37 | 9.95 | 100% | 0.38 | 1.28 | 2.70 | 117.0 → 114.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.35 | 1.31 | 11.26 | 99% | 0.40 | 1.05 | 2.95 | 141.2 → 137.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.38 | 1.29 | 9.06 | 99% | 0.40 | 1.04 | 2.88 | 143.3 → 139.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.38 | 1.36 | 9.72 | 99% | 0.37 | 1.05 | 2.69 | 146.0 → 143.0 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.33 | 1.21 | 6.87 | 100% | 0.31 | 1.01 | 2.67 | 105.8 → 104.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.41 | 1.36 | 9.90 | 99% | 0.40 | 1.11 | 2.78 | 145.7 → 143.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 2.94 | 13.37 | 99% | 1.92 | 2.36 | 2.97 | 147.0 → 143.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.29 | 1.90 | 6.39 | 100% | 0.38 | 1.04 | 2.69 | 67.9 → 67.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.165 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
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
| this editor | 368 | 0.43 | 1.55 | 12.77 | 99% | 0.46 | 1.21 | 3.31 | 142.1 → 143.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.38 | 4.31 | 21.85 | 96% | 0.53 | 3.33 | 3.62 | 543.5 → 560.8 | 12 | 3.5 / 31 |

