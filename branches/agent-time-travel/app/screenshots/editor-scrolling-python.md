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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 2.50 | 9.62 | 100% | 0.35 | 2.25 | 2.68 | 121.2 → 129.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.54 | 3.28 | 12.21 | 100% | 0.64 | 2.40 | 3.47 | 159.0 → 155.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.50 | 1.53 | 12.75 | 99% | 0.54 | 1.13 | 2.92 | 205.3 → 190.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.40 | 1.34 | 9.16 | 99% | 0.39 | 1.10 | 2.75 | 143.0 → 144.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.33 | 1.23 | 10.97 | 99% | 0.37 | 1.00 | 2.76 | 117.7 → 115.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.37 | 1.26 | 12.48 | 99% | 0.39 | 1.06 | 2.81 | 142.5 → 138.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.39 | 1.37 | 11.08 | 99% | 0.39 | 1.11 | 2.85 | 144.9 → 141.5 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.47 | 2.65 | 14.26 | 99% | 0.44 | 1.42 | 2.81 | 147.7 → 144.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.43 | 2.80 | 9.33 | 100% | 0.42 | 2.30 | 2.81 | 107.2 → 105.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.41 | 1.35 | 11.64 | 99% | 0.40 | 1.11 | 2.80 | 147.4 → 144.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.39 | 1.34 | 11.43 | 99% | 0.38 | 1.10 | 2.57 | 147.3 → 144.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.22 | 1.11 | 8.80 | 100% | 0.30 | 0.93 | 2.39 | 67.9 → 67.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.158 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| MeasuringLayoutable Grid | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.40 | 1.39 | 13.51 | 99% | 0.40 | 1.16 | 2.80 | 143.5 → 144.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.52 | 5.41 | 24.94 | 96% | 0.74 | 4.60 | 4.59 | 552.5 → 580.8 | 14 | 3.5 / 31 |

