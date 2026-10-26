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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.39 | 2.53 | 7.52 | 100% | 0.30 | 2.26 | 3.39 | 112.4 → 113.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.45 | 2.72 | 10.24 | 99% | 0.44 | 2.31 | 2.87 | 151.4 → 148.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.44 | 1.86 | 11.28 | 99% | 0.39 | 1.57 | 2.44 | 186.9 → 171.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.35 | 1.64 | 8.42 | 100% | 0.29 | 1.48 | 2.37 | 130.0 → 127.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.28 | 1.56 | 7.25 | 100% | 0.27 | 1.43 | 2.21 | 106.5 → 99.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.33 | 1.59 | 11.24 | 99% | 0.30 | 1.44 | 2.33 | 132.4 → 124.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.34 | 1.67 | 8.58 | 100% | 0.31 | 1.52 | 2.38 | 132.3 → 124.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.46 | 2.85 | 10.10 | 99% | 0.44 | 2.35 | 2.73 | 132.1 → 124.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.33 | 1.64 | 7.34 | 100% | 0.26 | 1.47 | 2.31 | 109.2 → 104.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.35 | 1.70 | 7.80 | 100% | 0.30 | 1.52 | 2.36 | 134.9 → 127.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.34 | 1.47 | 8.41 | 100% | 0.30 | 1.29 | 2.09 | 134.9 → 126.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.20 | 1.46 | 7.22 | 100% | 0.24 | 1.37 | 1.76 | 70.7 → 67.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.102 |
| AttachingStyle - | 0.007 |
| ArrangingLayoutable TextView | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| FindingResource - | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable LineDrawing | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.27 | 1.48 | 9.40 | 98% | 0.23 | 1.24 | 2.88 | 76.4 → 98.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 0 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.42 | 2.39 | 8.96 | 100% | 0.38 | 1.75 | 2.85 | 131.4 → 127.7 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.59 | 1.97 | 13.46 | 98% | 0.38 | 1.74 | 2.47 | 344.4 → 337.9 | 8 | 1.1 / 4 |

