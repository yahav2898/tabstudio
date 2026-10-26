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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.97 | 2.99 | 12.29 | 99% | 0.57 | 2.37 | 5.40 | 120.6 → 121.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.27 | 3.86 | 17.59 | 99% | 0.85 | 2.82 | 5.81 | 158.2 → 156.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.42 | 3.62 | 19.94 | 99% | 1.04 | 2.68 | 5.60 | 193.7 → 178.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.16 | 3.29 | 12.86 | 99% | 0.75 | 2.56 | 5.60 | 139.2 → 135.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.86 | 2.79 | 8.98 | 99% | 0.68 | 2.21 | 5.50 | 114.3 → 108.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.06 | 3.45 | 18.16 | 99% | 0.75 | 2.50 | 5.57 | 140.1 → 132.6 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.08 | 3.11 | 11.24 | 99% | 0.73 | 2.43 | 5.47 | 140.1 → 132.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.09 | 3.05 | 14.89 | 99% | 0.74 | 2.36 | 5.48 | 139.9 → 131.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.88 | 2.76 | 8.53 | 100% | 0.62 | 2.22 | 5.06 | 117.3 → 112.2 | 1 | 1.2 / 30 |
| without squiggles | 368 | 1.17 | 3.48 | 13.19 | 99% | 0.80 | 2.64 | 5.55 | 142.5 → 135.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.96 | 3.07 | 83.36 | 99% | 0.63 | 2.42 | 4.87 | 142.5 → 134.5 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.64 | 2.40 | 7.16 | 100% | 0.56 | 1.95 | 4.06 | 78.5 → 75.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.243 |
| ArrangingLayoutable LineDrawing | 0.053 |
| AttachingStyle - | 0.050 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.97 | 2.90 | 3.07 | 100% | 0.59 | 2.19 | 6.09 | 84.9 → 107.6 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 242 frames at 120 Hz; the text stood still on 0 of them, 263 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.25 | 4.80 | 83.33 | 99% | 0.87 | 4.18 | 5.65 | 139.0 → 136.8 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.61 | 3.65 | 20.51 | 98% | 0.90 | 2.78 | 5.53 | 350.4 → 343.6 | 8 | 1.1 / 4 |

