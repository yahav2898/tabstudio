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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.75 | 2.74 | 13.21 | 99% | 0.50 | 2.27 | 3.31 | 84.8 → 85.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 2.97 | 14.86 | 99% | 0.62 | 2.36 | 3.38 | 121.9 → 122.4 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.70 | 2.72 | 15.87 | 99% | 0.73 | 2.23 | 3.25 | 157.9 → 142.9 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.60 | 2.54 | 16.39 | 99% | 0.47 | 2.11 | 3.03 | 102.2 → 99.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.57 | 2.88 | 14.75 | 100% | 0.92 | 2.22 | 3.14 | 80.3 → 73.5 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.51 | 2.38 | 14.84 | 100% | 0.48 | 2.01 | 2.95 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.67 | 2.69 | 17.97 | 99% | 0.55 | 2.16 | 3.29 | 104.8 → 96.6 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.63 | 2.54 | 17.00 | 99% | 0.47 | 2.10 | 3.08 | 104.2 → 95.9 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.61 | 2.63 | 12.12 | 99% | 0.47 | 2.18 | 3.11 | 82.6 → 77.2 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.76 | 3.03 | 14.12 | 99% | 0.65 | 2.41 | 3.37 | 107.3 → 99.3 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.57 | 2.82 | 13.99 | 100% | 0.56 | 2.21 | 2.94 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.28 | 2.11 | 10.02 | 100% | 0.44 | 1.85 | 1.94 | 46.2 → 42.6 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.168 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.68 | 2.48 | 2.71 | 100% | 0.39 | 2.10 | 3.11 | 49.2 → 73.2 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 33 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.72 | 2.90 | 15.38 | 99% | 0.56 | 2.34 | 3.29 | 103.2 → 99.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.03 | 3.34 | 19.28 | 98% | 0.72 | 2.62 | 3.35 | 308.8 → 301.0 | 7 | 1.1 / 4 |

