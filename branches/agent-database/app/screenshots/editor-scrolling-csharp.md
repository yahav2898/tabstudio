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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.06 | 3.15 | 13.96 | 99% | 0.58 | 2.60 | 5.09 | 117.2 → 118.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.16 | 2.93 | 13.53 | 99% | 0.76 | 2.21 | 4.91 | 155.2 → 153.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.36 | 3.16 | 16.39 | 99% | 0.95 | 2.23 | 5.22 | 192.0 → 175.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.18 | 3.42 | 13.96 | 99% | 0.79 | 2.60 | 5.16 | 135.4 → 132.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.87 | 2.57 | 12.41 | 99% | 0.67 | 1.92 | 4.76 | 110.8 → 104.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.01 | 2.79 | 14.49 | 99% | 0.73 | 2.12 | 4.87 | 136.6 → 128.9 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 2.71 | 11.85 | 99% | 0.71 | 2.09 | 4.92 | 136.8 → 128.7 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.15 | 4.52 | 12.52 | 99% | 0.83 | 2.74 | 5.18 | 136.9 → 128.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.04 | 2.72 | 9.97 | 99% | 0.63 | 2.12 | 4.70 | 113.3 → 108.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 1.08 | 2.92 | 12.18 | 99% | 0.73 | 2.22 | 5.01 | 139.3 → 132.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.13 | 4.58 | 12.87 | 99% | 0.78 | 2.80 | 4.87 | 139.9 → 131.9 | 5 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.75 | 2.32 | 7.05 | 100% | 0.61 | 1.80 | 3.77 | 74.9 → 71.7 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.268 |
| ArrangingLayoutable Grid | 0.047 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.016 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.94 | 2.64 | 2.85 | 100% | 0.52 | 1.99 | 5.07 | 80.7 → 102.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 242 frames at 120 Hz; the text stood still on 0 of them, 264 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.13 | 4.80 | 15.22 | 99% | 0.77 | 4.13 | 5.22 | 135.3 → 133.1 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.59 | 3.29 | 18.53 | 98% | 0.83 | 2.52 | 5.02 | 349.9 → 343.1 | 7 | 1.1 / 4 |

