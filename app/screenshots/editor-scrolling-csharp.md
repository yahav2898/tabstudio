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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 2.60 | 13.82 | 100% | 0.44 | 2.17 | 3.27 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.88 | 2.94 | 16.13 | 99% | 0.60 | 2.41 | 3.52 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.72 | 2.79 | 14.91 | 99% | 0.75 | 2.21 | 3.20 | 158.1 → 143.0 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.57 | 2.52 | 13.31 | 99% | 0.52 | 2.17 | 3.29 | 102.2 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.45 | 2.36 | 9.70 | 100% | 0.59 | 2.08 | 3.19 | 80.5 → 73.6 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.54 | 3.12 | 32.82 | 99% | 1.21 | 2.26 | 3.22 | 105.1 → 96.6 | 4 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.57 | 2.92 | 11.30 | 99% | 0.52 | 2.37 | 3.18 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.40 | 2.34 | 13.15 | 99% | 0.47 | 1.94 | 3.21 | 104.2 → 96.0 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.39 | 2.58 | 12.40 | 99% | 0.46 | 2.10 | 2.97 | 82.7 → 77.3 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.37 | 2.16 | 13.18 | 99% | 0.34 | 1.88 | 2.53 | 107.4 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.47 | 2.55 | 17.79 | 99% | 0.47 | 2.11 | 2.72 | 107.6 → 99.2 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.29 | 2.09 | 11.01 | 100% | 0.81 | 1.82 | 2.17 | 46.5 → 42.8 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.171 |
| AttachingStyle - | 0.059 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.74 | 2.87 | 4.99 | 100% | 0.51 | 2.29 | 3.53 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 25 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.46 | 2.31 | 19.38 | 99% | 0.45 | 2.00 | 3.13 | 103.3 → 99.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.66 | 2.58 | 16.57 | 98% | 0.47 | 2.15 | 2.95 | 308.9 → 301.0 | 7 | 1.1 / 4 |

