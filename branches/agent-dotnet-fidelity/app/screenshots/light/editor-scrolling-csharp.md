# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.00 | 3.75 | 15.44 | 99% | 0.61 | 3.15 | 4.99 | 86.1 → 87.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.28 | 4.11 | 20.81 | 99% | 1.37 | 3.26 | 5.19 | 123.5 → 121.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.34 | 4.10 | 19.54 | 99% | 1.09 | 3.22 | 5.23 | 158.6 → 143.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.09 | 3.73 | 17.96 | 99% | 0.75 | 3.04 | 4.96 | 103.3 → 100.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.95 | 3.57 | 12.34 | 100% | 0.83 | 2.83 | 4.93 | 80.6 → 73.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.10 | 3.66 | 14.99 | 100% | 0.78 | 2.96 | 5.02 | 105.9 → 97.6 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.09 | 3.69 | 16.80 | 99% | 0.78 | 2.94 | 5.03 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.11 | 3.73 | 18.86 | 100% | 0.79 | 3.02 | 4.97 | 105.1 → 96.9 | 2 | 1.2 / 30 |
| without coloring | 368 | 1.09 | 3.71 | 11.53 | 99% | 0.75 | 3.08 | 4.85 | 83.5 → 78.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.13 | 3.78 | 18.37 | 99% | 0.81 | 3.02 | 5.33 | 108.4 → 100.4 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.09 | 3.73 | 17.18 | 99% | 0.79 | 2.97 | 4.46 | 108.5 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.74 | 3.25 | 11.05 | 100% | 1.29 | 2.63 | 3.72 | 46.0 → 42.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.463 |
| ArrangingLayoutable CachedLineNumberMargin | 0.043 |
| AttachingStyle - | 0.037 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable LineDrawing | 0.023 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| FindingResource - | 0.015 |
| ArrangingLayoutable RepeatButton | 0.015 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.01 | 3.45 | 3.64 | 100% | 0.59 | 2.85 | 5.36 | 50.1 → 72.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2244 ms over 189 frames at 120 Hz; the text stood still on 0 of them, 223 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.14 | 3.70 | 18.58 | 99% | 0.80 | 2.99 | 5.08 | 104.3 → 101.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.62 | 4.50 | 22.75 | 98% | 0.94 | 3.43 | 5.17 | 316.6 → 308.6 | 8 | 1.1 / 4 |

