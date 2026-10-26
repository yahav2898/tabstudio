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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 1.47 | 8.61 | 100% | 0.32 | 1.21 | 3.17 | 123.8 → 131.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 1.61 | 11.73 | 99% | 0.47 | 1.21 | 3.42 | 160.5 → 158.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.65 | 1.81 | 12.32 | 99% | 0.67 | 1.27 | 3.75 | 207.6 → 192.4 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 3.40 | 67.92 | 99% | 2.37 | 2.46 | 3.86 | 146.1 → 147.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.42 | 1.40 | 11.20 | 99% | 0.45 | 1.10 | 3.57 | 120.1 → 118.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.48 | 1.47 | 13.00 | 99% | 0.47 | 1.16 | 3.55 | 145.0 → 141.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.49 | 1.51 | 12.19 | 99% | 0.47 | 1.23 | 3.49 | 147.2 → 144.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.50 | 1.57 | 10.07 | 99% | 0.47 | 1.24 | 3.41 | 150.2 → 146.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 2.62 | 9.37 | 99% | 0.41 | 2.31 | 3.62 | 109.3 → 108.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.55 | 2.52 | 9.37 | 99% | 0.50 | 1.40 | 3.82 | 150.3 → 146.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.50 | 1.51 | 12.11 | 99% | 0.47 | 1.18 | 3.26 | 149.8 → 146.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.29 | 1.19 | 8.90 | 100% | 0.33 | 0.99 | 3.04 | 70.1 → 69.6 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.177 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable Grid | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.51 | 1.55 | 12.48 | 99% | 0.47 | 1.22 | 3.57 | 145.9 → 147.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 2.26 | 6.13 | 22.99 | 96% | 2.34 | 5.31 | 4.12 | 552.6 → 567.6 | 13 | 3.5 / 31 |

