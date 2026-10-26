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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.68 | 1.97 | 10.68 | 99% | 0.41 | 1.57 | 3.85 | 112.9 → 114.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 2.37 | 11.80 | 99% | 0.66 | 1.78 | 4.25 | 151.6 → 149.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.09 | 4.44 | 15.67 | 99% | 2.65 | 2.77 | 4.43 | 187.2 → 171.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.80 | 2.11 | 11.60 | 99% | 0.53 | 1.59 | 3.98 | 131.1 → 128.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.68 | 1.89 | 13.46 | 99% | 0.55 | 1.45 | 3.79 | 107.3 → 100.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 2.13 | 15.30 | 99% | 0.57 | 1.61 | 4.07 | 133.4 → 125.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.86 | 2.85 | 14.33 | 99% | 0.61 | 1.82 | 4.28 | 133.2 → 125.7 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.79 | 2.97 | 10.88 | 99% | 0.63 | 1.78 | 3.93 | 132.7 → 124.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 1.93 | 11.72 | 99% | 0.48 | 1.52 | 3.69 | 110.3 → 105.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.81 | 2.12 | 12.79 | 99% | 0.57 | 1.64 | 4.05 | 135.9 → 128.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 2.18 | 12.89 | 99% | 0.57 | 1.70 | 3.76 | 135.7 → 127.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.01 | 9.66 | 99% | 0.54 | 1.46 | 3.26 | 71.5 → 68.0 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.283 |
| FindingResource - | 0.053 |
| ArrangingLayoutable TextView | 0.029 |
| AttachingStyle - | 0.021 |
| ArrangingLayoutable ScrollingTextLayer | 0.017 |
| MeasuringLayoutable Grid | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.88 | 2.08 | 2.44 | 100% | 0.50 | 1.64 | 4.45 | 78.2 → 100.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 16 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 2.29 | 12.07 | 99% | 0.63 | 1.72 | 4.40 | 131.6 → 129.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.29 | 4.71 | 18.80 | 98% | 0.77 | 4.15 | 4.38 | 343.8 → 338.1 | 9 | 1.1 / 4 |

