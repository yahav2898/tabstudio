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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 1.92 | 10.15 | 100% | 0.42 | 1.52 | 3.37 | 112.9 → 113.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 1.78 | 10.68 | 99% | 0.53 | 1.37 | 2.91 | 151.1 → 148.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.79 | 2.13 | 13.93 | 99% | 0.63 | 1.63 | 3.45 | 187.4 → 171.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.94 | 2.49 | 13.39 | 99% | 0.63 | 1.85 | 3.99 | 131.7 → 128.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.75 | 11.95 | 99% | 0.54 | 2.26 | 3.19 | 107.6 → 100.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.39 | 13.39 | 99% | 0.49 | 1.74 | 3.33 | 133.7 → 125.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.73 | 2.29 | 14.94 | 99% | 0.60 | 1.75 | 3.55 | 133.1 → 125.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.68 | 2.00 | 13.10 | 100% | 0.48 | 1.56 | 3.07 | 132.4 → 124.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 1.79 | 9.28 | 100% | 0.43 | 1.44 | 2.85 | 109.6 → 104.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.86 | 2.42 | 12.83 | 99% | 0.59 | 1.84 | 3.68 | 135.9 → 128.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.71 | 2.79 | 13.17 | 99% | 0.52 | 2.29 | 3.15 | 135.3 → 128.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.49 | 75.86 | 100% | 0.46 | 1.33 | 2.28 | 71.0 → 67.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.238 |
| ArrangingLayoutable TextView | 0.013 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 1.65 | 2.40 | 100% | 0.47 | 1.38 | 3.32 | 77.5 → 98.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.54 | 1.74 | 12.39 | 99% | 0.44 | 1.41 | 2.97 | 131.3 → 128.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 2.68 | 16.58 | 98% | 0.68 | 2.03 | 3.57 | 344.9 → 338.7 | 7 | 1.1 / 4 |

