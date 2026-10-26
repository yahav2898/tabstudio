# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.03 | 3.07 | 12.20 | 99% | 0.58 | 2.44 | 5.15 | 117.2 → 117.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.20 | 2.98 | 13.34 | 99% | 0.75 | 2.28 | 5.03 | 155.7 → 153.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.29 | 3.11 | 15.27 | 99% | 0.89 | 2.32 | 5.14 | 192.1 → 175.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.22 | 3.77 | 16.59 | 99% | 0.78 | 2.76 | 5.23 | 135.2 → 132.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.94 | 2.68 | 8.81 | 99% | 0.67 | 2.05 | 4.87 | 110.9 → 103.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.05 | 2.77 | 16.24 | 99% | 0.70 | 2.12 | 4.98 | 136.9 → 128.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.11 | 2.96 | 12.63 | 99% | 0.71 | 2.28 | 5.13 | 137.1 → 129.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.12 | 3.75 | 12.98 | 99% | 0.81 | 2.61 | 5.16 | 137.0 → 128.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.01 | 2.79 | 10.95 | 99% | 0.64 | 2.14 | 4.84 | 113.6 → 108.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.09 | 2.91 | 12.34 | 99% | 0.72 | 2.25 | 5.14 | 139.4 → 132.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.13 | 3.37 | 17.85 | 99% | 0.77 | 2.57 | 4.72 | 139.4 → 132.0 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.81 | 2.44 | 8.43 | 99% | 0.62 | 1.87 | 3.90 | 75.1 → 71.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.275 |
| ArrangingLayoutable NumberDrawing | 0.043 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.95 | 2.67 | 2.83 | 100% | 0.49 | 2.03 | 5.08 | 80.7 → 102.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 240 frames at 120 Hz; the text stood still on 0 of them, 266 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.13 | 3.14 | 16.08 | 99% | 0.73 | 2.40 | 5.19 | 135.7 → 132.9 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.59 | 3.41 | 18.74 | 98% | 0.85 | 2.60 | 5.05 | 349.9 → 343.3 | 8 | 1.1 / 4 |

