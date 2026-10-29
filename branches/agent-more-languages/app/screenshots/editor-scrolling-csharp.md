# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.05 | 3.73 | 12.89 | 99% | 0.57 | 3.13 | 4.48 | 125.1 → 126.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.21 | 3.95 | 14.58 | 99% | 0.75 | 3.18 | 4.53 | 163.9 → 161.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.25 | 3.96 | 17.13 | 99% | 0.85 | 3.17 | 4.48 | 200.0 → 184.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.16 | 5.17 | 16.05 | 99% | 0.76 | 3.25 | 4.61 | 143.7 → 140.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.98 | 3.66 | 12.59 | 99% | 0.67 | 3.07 | 4.53 | 119.5 → 111.7 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.97 | 3.38 | 19.69 | 99% | 0.65 | 2.85 | 4.42 | 144.8 → 136.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.12 | 3.72 | 17.36 | 99% | 0.71 | 3.03 | 4.76 | 145.8 → 137.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.92 | 3.54 | 14.53 | 99% | 0.65 | 2.93 | 4.30 | 143.5 → 136.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.06 | 5.42 | 11.60 | 99% | 0.67 | 3.16 | 4.36 | 121.8 → 117.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.00 | 3.55 | 15.80 | 99% | 0.62 | 3.02 | 4.49 | 147.4 → 139.4 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.93 | 3.37 | 13.23 | 99% | 0.60 | 2.85 | 3.75 | 146.8 → 138.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 3.10 | 10.07 | 99% | 0.57 | 2.62 | 3.36 | 82.3 → 79.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.320 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.012 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.94 | 3.28 | 3.66 | 100% | 0.47 | 2.82 | 4.67 | 88.5 → 111.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 230 frames at 120 Hz; the text stood still on 0 of them, 262 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.92 | 3.76 | 12.65 | 99% | 0.65 | 3.03 | 4.35 | 143.4 → 139.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.43 | 4.05 | 19.63 | 98% | 0.73 | 3.32 | 4.53 | 357.6 → 351.1 | 8 | 1.1 / 4 |

