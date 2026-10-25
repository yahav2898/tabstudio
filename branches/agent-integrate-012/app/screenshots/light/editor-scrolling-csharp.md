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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 1.55 | 7.42 | 100% | 0.30 | 1.31 | 3.34 | 109.5 → 110.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.58 | 1.69 | 9.68 | 99% | 0.47 | 1.33 | 3.37 | 147.1 → 144.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.75 | 3.09 | 12.63 | 99% | 0.85 | 2.40 | 3.80 | 182.9 → 167.3 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.54 | 1.74 | 9.40 | 100% | 0.42 | 1.35 | 3.52 | 127.3 → 124.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 1.43 | 8.69 | 100% | 0.39 | 1.14 | 3.23 | 103.7 → 97.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.50 | 1.51 | 11.27 | 99% | 0.42 | 1.20 | 3.33 | 129.2 → 121.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.50 | 1.54 | 9.57 | 99% | 0.41 | 1.25 | 3.36 | 129.3 → 121.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.75 | 12.84 | 99% | 0.50 | 2.11 | 3.57 | 128.7 → 121.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.49 | 1.91 | 6.72 | 100% | 0.39 | 1.59 | 3.34 | 107.0 → 102.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.52 | 1.99 | 9.73 | 99% | 0.41 | 1.67 | 3.40 | 132.1 → 124.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.50 | 1.52 | 9.79 | 99% | 0.41 | 1.23 | 3.07 | 131.8 → 123.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 1.31 | 8.39 | 100% | 0.35 | 1.04 | 2.60 | 68.5 → 64.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.200 |
| AttachingStyle - | 0.040 |
| ArrangingLayoutable LineDrawing | 0.017 |
| ArrangingLayoutable NumberDrawing | 0.014 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.51 | 3.48 | 4.06 | 100% | 0.38 | 3.29 | 3.69 | 74.9 → 96.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 64 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.52 | 1.57 | 11.21 | 100% | 0.40 | 1.26 | 3.35 | 127.6 → 124.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.83 | 2.03 | 13.55 | 98% | 0.48 | 1.54 | 3.43 | 338.8 → 332.2 | 7 | 1.1 / 4 |

