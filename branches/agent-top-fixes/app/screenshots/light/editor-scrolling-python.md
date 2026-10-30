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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.35 | 2.12 | 8.80 | 100% | 0.28 | 1.87 | 2.93 | 95.3 → 102.3 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.44 | 2.25 | 14.27 | 99% | 0.72 | 1.95 | 2.98 | 131.3 → 129.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.55 | 2.43 | 15.79 | 99% | 1.05 | 2.02 | 3.16 | 177.9 → 163.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 2.43 | 15.03 | 99% | 0.49 | 2.07 | 3.23 | 116.6 → 118.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 2.59 | 14.40 | 99% | 1.22 | 2.15 | 3.15 | 91.9 → 90.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.44 | 2.85 | 12.21 | 99% | 0.81 | 2.29 | 3.10 | 116.5 → 112.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.36 | 2.14 | 15.92 | 99% | 0.81 | 1.90 | 3.04 | 118.6 → 115.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.39 | 2.17 | 37.41 | 99% | 0.80 | 1.89 | 2.99 | 121.5 → 118.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.40 | 2.24 | 10.59 | 99% | 0.72 | 1.95 | 2.79 | 81.3 → 79.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.60 | 2.58 | 14.16 | 99% | 0.57 | 2.13 | 3.14 | 121.2 → 118.0 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.54 | 2.87 | 4.76 | 100% | 1.28 | 2.19 | 2.93 | 121.3 → 117.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.36 | 14.01 | 100% | 0.65 | 1.95 | 2.50 | 43.4 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.163 |
| AttachingStyle - | 0.081 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| MeasuringLayoutable Grid | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin, TextMate's tokenizing on every scroll put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.42 | 2.31 | 13.85 | 99% | 0.72 | 1.98 | 3.16 | 117.3 → 118.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.49 | 4.56 | 25.17 | 96% | 0.55 | 3.97 | 5.67 | 582.0 → 602.1 | 13 | 3.5 / 31 |

