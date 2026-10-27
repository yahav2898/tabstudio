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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.62 | 2.95 | 12.81 | 99% | 0.38 | 2.60 | 4.29 | 119.8 → 121.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 3.20 | 15.85 | 99% | 0.63 | 2.75 | 4.49 | 158.3 → 155.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 3.29 | 17.17 | 99% | 0.71 | 2.72 | 4.52 | 193.7 → 178.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 3.00 | 16.83 | 99% | 0.61 | 2.60 | 4.53 | 137.8 → 135.6 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 3.36 | 17.30 | 99% | 0.62 | 2.63 | 4.42 | 115.4 → 108.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.66 | 2.38 | 17.05 | 99% | 0.54 | 1.93 | 4.27 | 140.2 → 132.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.69 | 2.39 | 13.91 | 99% | 0.54 | 1.97 | 4.33 | 140.2 → 131.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 2.92 | 14.17 | 99% | 0.54 | 2.56 | 4.18 | 139.3 → 131.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 3.22 | 13.12 | 99% | 0.57 | 2.74 | 4.37 | 117.4 → 112.7 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.80 | 3.27 | 13.64 | 99% | 0.53 | 2.77 | 4.69 | 143.3 → 134.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.68 | 2.64 | 13.03 | 99% | 0.53 | 2.37 | 3.76 | 142.6 → 134.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 2.30 | 10.81 | 99% | 0.45 | 1.80 | 3.19 | 79.4 → 75.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.183 |
| ArrangingLayoutable TextView | 0.012 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.58 | 2.81 | 9.22 | 98% | 0.39 | 2.44 | 4.64 | 85.1 → 106.8 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 258 frames at 120 Hz; the text stood still on 0 of them, 251 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.81 | 4.39 | 15.93 | 99% | 0.67 | 2.86 | 4.78 | 139.2 → 135.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.12 | 3.45 | 20.50 | 98% | 0.66 | 2.95 | 4.54 | 350.9 → 344.4 | 7 | 1.1 / 4 |

