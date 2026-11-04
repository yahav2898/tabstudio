# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 3.11 | 10.23 | 100% | 0.46 | 2.69 | 4.28 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 3.59 | 16.42 | 99% | 0.68 | 2.88 | 4.70 | 121.3 → 121.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.92 | 3.66 | 16.24 | 99% | 1.09 | 2.87 | 4.65 | 156.8 → 141.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 3.85 | 17.94 | 99% | 0.74 | 3.02 | 4.97 | 101.7 → 98.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 3.54 | 12.32 | 100% | 0.85 | 2.80 | 5.00 | 79.7 → 73.0 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.75 | 3.36 | 16.56 | 99% | 0.72 | 2.84 | 4.78 | 104.3 → 96.1 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.90 | 3.66 | 16.90 | 99% | 0.78 | 2.96 | 4.93 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.81 | 3.48 | 97.00 | 99% | 0.68 | 2.87 | 5.06 | 103.6 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.68 | 3.10 | 12.83 | 99% | 0.53 | 2.66 | 4.26 | 82.2 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.69 | 3.17 | 12.88 | 100% | 0.62 | 2.71 | 4.43 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.80 | 5.05 | 12.79 | 99% | 1.01 | 2.92 | 4.18 | 106.8 → 98.7 | 4 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.81 | 8.19 | 100% | 1.11 | 2.41 | 3.32 | 46.0 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 63.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.277 |
| AttachingStyle - | 0.062 |
| ArrangingLayoutable TextView | 0.016 |
| FindingResource - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.64 | 3.38 | 3.56 | 100% | 0.46 | 2.68 | 4.83 | 49.2 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 216 frames at 120 Hz; the text stood still on 0 of them, 249 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.79 | 3.45 | 22.00 | 99% | 0.64 | 2.88 | 5.11 | 102.7 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.21 | 3.81 | 18.48 | 98% | 0.74 | 3.12 | 4.52 | 307.6 → 299.8 | 7 | 1.1 / 4 |

