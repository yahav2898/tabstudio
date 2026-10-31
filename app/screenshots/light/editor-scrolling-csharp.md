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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.64 | 13.53 | 100% | 0.47 | 2.10 | 3.09 | 84.4 → 85.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 4.13 | 16.50 | 100% | 0.99 | 2.52 | 3.57 | 121.3 → 121.7 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 2.76 | 15.93 | 99% | 1.19 | 2.24 | 3.31 | 156.7 → 141.7 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 2.49 | 13.99 | 99% | 0.48 | 2.06 | 3.10 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 2.30 | 14.40 | 99% | 0.51 | 1.92 | 2.89 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.57 | 2.43 | 18.35 | 99% | 0.53 | 2.04 | 3.09 | 104.3 → 96.0 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.60 | 2.53 | 16.82 | 99% | 0.51 | 2.08 | 3.07 | 104.1 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.59 | 2.66 | 12.71 | 100% | 0.54 | 2.11 | 2.99 | 103.5 → 95.3 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.63 | 3.81 | 17.49 | 99% | 1.47 | 2.31 | 3.17 | 82.2 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.62 | 2.62 | 14.31 | 99% | 0.49 | 2.14 | 3.15 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.58 | 2.44 | 16.21 | 99% | 0.46 | 2.07 | 2.64 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.14 | 13.45 | 100% | 1.12 | 1.84 | 2.10 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.250 |
| AttachingStyle - | 0.018 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 2.45 | 2.55 | 100% | 0.40 | 1.92 | 3.08 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 37 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 3.06 | 30.88 | 99% | 0.68 | 2.41 | 3.21 | 102.6 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.83 | 2.94 | 20.69 | 98% | 0.58 | 2.34 | 3.25 | 307.5 → 299.7 | 7 | 1.1 / 4 |

