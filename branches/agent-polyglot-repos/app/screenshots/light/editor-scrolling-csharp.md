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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.51 | 2.62 | 9.38 | 99% | 0.35 | 2.36 | 2.78 | 118.1 → 119.2 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.62 | 3.14 | 14.41 | 100% | 0.60 | 2.29 | 3.28 | 156.2 → 153.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.59 | 1.84 | 13.48 | 99% | 0.50 | 1.45 | 2.87 | 190.2 → 175.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.53 | 1.65 | 9.16 | 99% | 0.36 | 1.37 | 2.75 | 135.3 → 132.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.40 | 1.53 | 8.43 | 99% | 0.38 | 1.24 | 2.63 | 111.9 → 105.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.44 | 1.50 | 12.49 | 99% | 0.39 | 1.26 | 2.66 | 137.4 → 129.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.42 | 1.56 | 8.62 | 99% | 0.34 | 1.25 | 2.59 | 137.4 → 129.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 3.10 | 11.72 | 100% | 0.51 | 2.33 | 2.94 | 137.4 → 129.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.73 | 7.88 | 100% | 0.33 | 1.43 | 2.74 | 115.1 → 109.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.52 | 1.74 | 10.19 | 99% | 0.39 | 1.43 | 2.81 | 140.2 → 132.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.45 | 1.55 | 9.52 | 99% | 0.37 | 1.27 | 2.34 | 139.5 → 131.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.38 | 6.55 | 100% | 0.32 | 1.15 | 1.98 | 76.5 → 72.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.120 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable TextView | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
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
| touch drag and flick (real touch input) | 46 | 0.41 | 1.60 | 9.26 | 98% | 0.33 | 1.28 | 2.92 | 82.6 → 103.9 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 3 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.53 | 3.24 | 9.86 | 99% | 0.56 | 2.36 | 2.87 | 136.1 → 133.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.61 | 1.64 | 12.98 | 98% | 0.35 | 1.36 | 2.47 | 346.8 → 340.2 | 7 | 1.1 / 4 |

