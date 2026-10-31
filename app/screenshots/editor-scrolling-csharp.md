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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 3.09 | 17.37 | 99% | 0.48 | 2.69 | 4.39 | 84.8 → 85.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 3.41 | 16.58 | 100% | 0.67 | 2.84 | 4.61 | 122.1 → 122.6 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 3.58 | 18.29 | 99% | 1.06 | 2.82 | 4.76 | 158.1 → 143.1 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.81 | 3.52 | 35.36 | 99% | 0.81 | 2.87 | 4.97 | 102.3 → 99.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 3.55 | 37.96 | 99% | 0.74 | 2.72 | 5.03 | 80.6 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.71 | 3.18 | 13.52 | 100% | 0.65 | 2.68 | 4.55 | 105.0 → 96.6 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.76 | 3.36 | 12.36 | 100% | 0.65 | 2.78 | 4.68 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.75 | 3.19 | 21.50 | 99% | 0.62 | 2.72 | 4.47 | 104.2 → 95.9 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.73 | 3.35 | 15.12 | 99% | 0.63 | 2.77 | 4.65 | 82.6 → 77.3 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.86 | 3.84 | 14.66 | 99% | 0.74 | 2.90 | 5.37 | 107.5 → 99.4 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.70 | 3.23 | 15.65 | 99% | 0.58 | 2.73 | 3.86 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.74 | 14.25 | 100% | 1.30 | 2.39 | 3.17 | 46.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.269 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable TextView | 0.015 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 3.14 | 3.32 | 100% | 0.46 | 2.54 | 4.48 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 200 frames at 120 Hz; the text stood still on 0 of them, 233 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.84 | 3.37 | 24.17 | 99% | 0.64 | 2.87 | 5.21 | 103.4 → 99.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.21 | 3.66 | 19.40 | 98% | 0.74 | 3.06 | 4.69 | 308.9 → 301.1 | 7 | 1.1 / 4 |

