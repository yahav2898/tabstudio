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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.61 | 20.14 | 100% | 0.47 | 2.22 | 5.18 | 84.8 → 85.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 3.44 | 69.88 | 99% | 0.79 | 2.56 | 5.43 | 122.0 → 122.5 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.04 | 3.17 | 20.33 | 99% | 1.02 | 2.48 | 5.46 | 157.9 → 142.9 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 3.05 | 19.17 | 99% | 0.67 | 2.43 | 5.27 | 102.2 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 2.96 | 11.21 | 100% | 0.64 | 2.31 | 5.14 | 80.4 → 73.5 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.71 | 2.67 | 19.98 | 100% | 0.65 | 2.24 | 5.07 | 105.0 → 96.6 | 1 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.83 | 3.03 | 66.90 | 99% | 0.98 | 2.35 | 5.27 | 104.8 → 96.6 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.89 | 12.28 | 99% | 0.66 | 2.34 | 5.11 | 104.2 → 95.9 | 1 | 1.2 / 29 |
| without coloring | 368 | 0.65 | 2.65 | 17.93 | 99% | 0.52 | 2.19 | 4.74 | 82.6 → 77.2 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.79 | 4.25 | 18.30 | 100% | 0.75 | 2.47 | 5.36 | 107.3 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.85 | 3.02 | 9.61 | 99% | 0.67 | 2.43 | 4.71 | 107.4 → 99.2 | 1 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 2.51 | 3.52 | 100% | 0.60 | 2.09 | 3.97 | 46.2 → 42.6 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.339 |
| AttachingStyle - | 0.021 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.63 | 2.72 | 2.88 | 100% | 0.46 | 2.17 | 5.13 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2245 ms over 222 frames at 120 Hz; the text stood still on 0 of them, 254 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 2.81 | 33.74 | 100% | 0.63 | 2.30 | 5.39 | 103.2 → 99.9 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.44 | 5.34 | 25.83 | 99% | 1.25 | 2.91 | 5.54 | 308.8 → 301.0 | 4 | 1.1 / 4 |

