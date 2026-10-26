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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.30 | 1.24 | 7.38 | 100% | 0.20 | 1.10 | 2.29 | 111.3 → 112.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.40 | 1.41 | 18.68 | 100% | 0.34 | 1.16 | 2.52 | 149.4 → 147.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.44 | 1.43 | 11.79 | 99% | 0.39 | 1.16 | 2.43 | 185.0 → 169.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.36 | 1.33 | 9.32 | 99% | 0.30 | 1.14 | 2.45 | 129.0 → 126.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.28 | 1.23 | 8.20 | 100% | 0.28 | 1.04 | 2.20 | 105.9 → 99.1 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.35 | 1.29 | 9.33 | 100% | 0.34 | 1.12 | 2.46 | 131.3 → 123.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.57 | 2.87 | 12.56 | 99% | 0.47 | 2.39 | 3.02 | 132.0 → 124.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.53 | 2.57 | 11.95 | 99% | 0.43 | 1.69 | 3.13 | 131.5 → 122.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.33 | 1.29 | 8.42 | 100% | 0.26 | 1.12 | 2.27 | 108.5 → 103.6 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.38 | 1.54 | 9.70 | 99% | 0.32 | 1.26 | 2.54 | 134.0 → 126.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.35 | 1.43 | 9.25 | 99% | 0.32 | 1.18 | 2.09 | 133.7 → 125.9 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.25 | 1.28 | 9.05 | 100% | 0.31 | 1.09 | 1.78 | 70.6 → 66.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.152 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.59 | 1.65 | 10.14 | 98% | 0.39 | 1.31 | 3.07 | 76.3 → 99.2 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 8 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.44 | 1.84 | 9.13 | 99% | 0.38 | 1.47 | 2.86 | 130.3 → 126.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.61 | 1.68 | 14.03 | 98% | 0.41 | 1.37 | 2.52 | 342.2 → 335.6 | 8 | 1.1 / 4 |

