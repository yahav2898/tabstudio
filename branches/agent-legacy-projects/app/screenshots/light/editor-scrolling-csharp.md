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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 2.28 | 11.24 | 100% | 0.43 | 1.92 | 4.56 | 119.4 → 120.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.00 | 2.75 | 16.19 | 99% | 0.74 | 2.16 | 5.07 | 157.2 → 155.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.03 | 2.80 | 15.75 | 99% | 0.81 | 2.10 | 4.95 | 192.6 → 177.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 4.38 | 15.60 | 99% | 0.81 | 2.64 | 5.13 | 137.6 → 135.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 2.35 | 11.84 | 99% | 0.59 | 1.84 | 4.54 | 113.8 → 107.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.87 | 2.55 | 18.41 | 99% | 0.64 | 2.01 | 4.87 | 139.3 → 131.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.56 | 13.52 | 99% | 0.64 | 2.06 | 4.84 | 139.3 → 131.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 3.37 | 17.27 | 100% | 0.75 | 2.61 | 4.65 | 138.6 → 131.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.72 | 2.22 | 11.18 | 99% | 0.51 | 1.85 | 4.38 | 116.8 → 111.9 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.69 | 2.21 | 11.54 | 99% | 0.54 | 1.82 | 4.33 | 141.7 → 134.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.20 | 11.69 | 99% | 0.55 | 1.83 | 4.10 | 141.7 → 133.9 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.00 | 8.60 | 100% | 0.49 | 1.66 | 3.46 | 77.7 → 74.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.682 |
| ArrangingLayoutable TextView | 0.053 |
| AttachingStyle - | 0.035 |
| ArrangingLayoutable CachedLineNumberMargin | 0.032 |
| ArrangingLayoutable ScrollingTextLayer | 0.020 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| FindingResource - | 0.013 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.81 | 2.25 | 2.36 | 100% | 0.46 | 1.88 | 4.79 | 84.9 → 106.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 201 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.29 | 12.44 | 99% | 0.59 | 1.90 | 4.59 | 137.6 → 134.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.32 | 3.64 | 19.45 | 98% | 0.76 | 2.91 | 4.97 | 349.0 → 343.3 | 9 | 1.1 / 4 |

