# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 803 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 1.78 | 9.83 | 100% | 0.35 | 1.44 | 3.98 | 77.3 → 78.1 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.62 | 2.06 | 11.55 | 100% | 0.57 | 1.54 | 4.03 | 115.0 → 112.7 | 7 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.67 | 2.07 | 11.84 | 99% | 0.70 | 1.48 | 4.17 | 150.9 → 135.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.52 | 1.77 | 8.20 | 100% | 0.53 | 1.41 | 4.06 | 94.8 → 91.8 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.40 | 1.67 | 8.32 | 100% | 0.46 | 1.32 | 3.84 | 71.6 → 64.6 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.49 | 1.66 | 9.80 | 100% | 0.49 | 1.35 | 3.85 | 97.4 → 89.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.51 | 1.97 | 10.66 | 99% | 0.51 | 1.43 | 4.02 | 97.3 → 89.1 | 6 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.51 | 1.82 | 5.95 | 100% | 0.49 | 1.39 | 3.82 | 96.5 → 88.4 | 5 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.80 | 10.44 | 99% | 0.45 | 1.37 | 3.80 | 74.6 → 69.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.53 | 1.81 | 7.73 | 100% | 0.48 | 1.43 | 3.96 | 99.9 → 91.8 | 6 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.48 | 1.78 | 9.98 | 100% | 0.48 | 1.41 | 3.58 | 100.1 → 91.7 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.27 | 1.40 | 3.51 | 100% | 0.41 | 1.12 | 2.94 | 36.0 → 32.3 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.154 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.55 | 2.01 | 3.87 | 100% | 0.42 | 1.52 | 4.32 | 41.0 → 63.1 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 22 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.54 | 1.82 | 9.95 | 100% | 0.48 | 1.43 | 4.10 | 95.6 → 92.5 | 6 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.94 | 4.33 | 12.61 | 98% | 0.65 | 2.67 | 4.08 | 309.3 → 301.3 | 10 | 1.1 / 4 |

