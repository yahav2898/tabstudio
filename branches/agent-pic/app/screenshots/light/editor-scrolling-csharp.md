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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 3.69 | 71.72 | 99% | 0.37 | 2.43 | 3.53 | 112.7 → 113.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 1.60 | 12.87 | 99% | 0.43 | 1.26 | 3.25 | 150.4 → 148.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.62 | 1.74 | 10.67 | 99% | 0.54 | 1.30 | 3.35 | 186.0 → 170.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.49 | 1.55 | 9.16 | 99% | 0.40 | 1.25 | 3.23 | 130.0 → 127.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.40 | 1.41 | 7.03 | 100% | 0.40 | 1.15 | 3.05 | 106.4 → 99.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 3.59 | 13.97 | 99% | 2.26 | 2.38 | 3.82 | 132.8 → 125.2 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.48 | 1.55 | 9.48 | 99% | 0.41 | 1.24 | 3.20 | 132.2 → 124.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.47 | 1.54 | 12.18 | 99% | 0.41 | 1.24 | 3.14 | 131.5 → 123.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.50 | 7.24 | 100% | 0.35 | 1.21 | 3.06 | 109.4 → 104.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.51 | 1.54 | 9.11 | 99% | 0.42 | 1.23 | 3.24 | 134.8 → 127.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.58 | 3.70 | 66.66 | 99% | 0.50 | 2.48 | 3.16 | 135.0 → 127.6 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 1.37 | 5.22 | 100% | 0.36 | 1.07 | 2.60 | 70.8 → 67.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.170 |
| ArrangingLayoutable TextView | 0.009 |
| AttachingStyle - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.42 | 1.39 | 1.56 | 100% | 0.29 | 1.13 | 3.30 | 76.3 → 99.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 2 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.51 | 1.48 | 9.46 | 99% | 0.40 | 1.22 | 3.25 | 130.6 → 127.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.02 | 4.75 | 67.67 | 98% | 2.53 | 3.69 | 4.15 | 343.4 → 337.5 | 8 | 1.1 / 4 |

