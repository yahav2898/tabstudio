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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 1.51 | 13.20 | 99% | 0.30 | 1.29 | 3.33 | 114.8 → 116.4 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 3.03 | 73.17 | 99% | 0.55 | 2.52 | 3.68 | 154.0 → 151.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.62 | 1.76 | 11.85 | 99% | 0.54 | 1.26 | 3.38 | 189.6 → 173.7 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.47 | 1.51 | 9.19 | 99% | 0.39 | 1.23 | 3.20 | 132.8 → 130.0 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.39 | 1.39 | 7.52 | 100% | 0.39 | 1.12 | 3.06 | 109.3 → 102.5 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.47 | 1.44 | 12.33 | 100% | 0.39 | 1.17 | 3.19 | 135.2 → 127.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.61 | 3.12 | 10.16 | 99% | 0.55 | 2.37 | 3.56 | 135.8 → 127.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.49 | 1.49 | 11.88 | 99% | 0.40 | 1.18 | 3.16 | 134.5 → 126.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.44 | 1.42 | 13.16 | 100% | 0.34 | 1.14 | 3.09 | 112.0 → 107.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.49 | 1.43 | 9.13 | 99% | 0.40 | 1.18 | 3.18 | 137.7 → 130.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.47 | 1.55 | 8.14 | 100% | 0.39 | 1.20 | 2.89 | 137.7 → 129.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 1.31 | 7.75 | 100% | 0.33 | 1.03 | 2.55 | 73.0 → 69.7 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.250 |
| AttachingStyle - | 0.031 |
| ArrangingLayoutable TextView | 0.024 |
| ArrangingLayoutable ScrollingTextLayer | 0.016 |
| RaisingRoutedEvent ScrollBar | 0.012 |
| MeasuringLayoutable CachedLineNumberMargin | 0.012 |
| FindingResource - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.57 | 2.60 | 3.76 | 100% | 0.35 | 2.32 | 3.70 | 79.6 → 101.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.49 | 1.47 | 8.12 | 100% | 0.40 | 1.21 | 3.21 | 133.4 → 130.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.79 | 1.90 | 13.58 | 98% | 0.47 | 1.47 | 3.30 | 346.8 → 340.2 | 8 | 1.1 / 4 |

