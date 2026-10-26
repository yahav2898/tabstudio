# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.88 | 4.09 | 11.62 | 99% | 0.57 | 2.66 | 5.03 | 119.9 → 121.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.77 | 2.41 | 10.39 | 99% | 0.63 | 1.87 | 4.52 | 157.3 → 154.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 2.38 | 12.95 | 99% | 0.74 | 1.82 | 4.26 | 192.1 → 176.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 2.13 | 10.92 | 99% | 0.52 | 1.79 | 4.12 | 137.0 → 134.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.45 | 10.18 | 99% | 0.57 | 1.73 | 4.27 | 113.8 → 107.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.79 | 3.01 | 20.50 | 99% | 0.57 | 2.51 | 4.63 | 140.0 → 131.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 2.09 | 11.00 | 99% | 0.49 | 1.71 | 4.12 | 139.0 → 131.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.65 | 2.11 | 12.74 | 99% | 0.50 | 1.70 | 4.05 | 138.3 → 130.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.01 | 8.08 | 100% | 0.41 | 1.67 | 3.91 | 116.7 → 111.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 4.30 | 75.75 | 99% | 0.62 | 3.78 | 4.70 | 141.9 → 134.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 2.06 | 10.45 | 99% | 0.53 | 1.72 | 3.64 | 141.3 → 133.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 1.75 | 7.47 | 100% | 0.41 | 1.44 | 3.03 | 77.5 → 74.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.206 |
| AttachingStyle - | 0.045 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.51 | 1.76 | 1.97 | 100% | 0.32 | 1.57 | 4.33 | 84.9 → 105.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 12 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 2.93 | 10.50 | 99% | 0.57 | 2.46 | 4.74 | 138.2 → 134.7 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.08 | 2.66 | 14.16 | 98% | 0.62 | 2.11 | 4.23 | 348.4 → 342.1 | 8 | 1.1 / 4 |

