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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.30 | 1.16 | 7.06 | 100% | 0.22 | 1.01 | 2.34 | 120.9 → 128.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.38 | 1.23 | 10.35 | 99% | 0.34 | 0.99 | 2.62 | 157.8 → 155.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.43 | 1.33 | 10.85 | 99% | 0.48 | 1.01 | 2.67 | 205.0 → 190.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.34 | 1.20 | 10.10 | 100% | 0.33 | 0.98 | 2.57 | 142.9 → 144.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.28 | 1.12 | 10.68 | 99% | 0.33 | 0.93 | 2.56 | 117.3 → 115.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.45 | 2.74 | 12.88 | 99% | 0.99 | 2.27 | 2.82 | 142.8 → 139.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.39 | 2.60 | 8.05 | 100% | 0.38 | 1.35 | 2.67 | 145.0 → 141.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.35 | 1.25 | 10.31 | 99% | 0.34 | 1.03 | 2.60 | 147.5 → 144.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.29 | 1.12 | 6.47 | 100% | 0.25 | 0.96 | 2.34 | 106.3 → 104.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.34 | 1.24 | 10.10 | 99% | 0.34 | 1.03 | 2.57 | 147.3 → 144.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.33 | 1.20 | 9.24 | 99% | 0.35 | 0.99 | 2.41 | 146.9 → 143.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.18 | 0.97 | 6.94 | 100% | 0.22 | 0.84 | 2.12 | 67.4 → 66.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.236 |
| AttachingStyle - | 0.020 |
| ArrangingLayoutable TextView | 0.018 |
| ArrangingLayoutable ScrollingTextLayer | 0.016 |
| ArrangingLayoutable PageHost | 0.010 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| MeasuringLayoutable Grid | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.42 | 3.27 | 10.97 | 99% | 0.60 | 2.22 | 3.00 | 144.1 → 144.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.08 | 2.80 | 20.51 | 96% | 0.41 | 2.36 | 2.75 | 547.9 → 566.8 | 13 | 3.5 / 31 |

