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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.31 | 1.20 | 3.62 | 100% | 0.29 | 1.02 | 2.92 | 77.3 → 78.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.37 | 1.65 | 17.04 | 100% | 0.45 | 1.08 | 3.55 | 115.0 → 112.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.41 | 2.47 | 9.21 | 99% | 0.55 | 1.10 | 2.83 | 150.9 → 135.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.33 | 1.31 | 4.55 | 100% | 0.36 | 1.03 | 3.05 | 94.7 → 91.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.28 | 1.32 | 3.88 | 100% | 0.41 | 0.95 | 2.62 | 71.5 → 64.6 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 0.32 | 1.26 | 7.26 | 100% | 0.32 | 1.01 | 2.67 | 97.4 → 89.0 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.32 | 1.35 | 6.27 | 100% | 0.34 | 1.00 | 2.48 | 97.2 → 89.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.32 | 1.29 | 9.10 | 99% | 0.40 | 1.02 | 2.65 | 96.5 → 88.4 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.28 | 1.17 | 6.20 | 100% | 0.33 | 0.94 | 2.40 | 74.5 → 69.1 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.35 | 1.20 | 4.57 | 100% | 0.32 | 1.03 | 2.57 | 99.9 → 91.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.31 | 1.22 | 9.09 | 100% | 0.34 | 1.01 | 2.31 | 100.0 → 91.7 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.17 | 0.99 | 3.63 | 100% | 0.27 | 0.83 | 1.82 | 36.0 → 32.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.114 |
| AttachingStyle - | 0.042 |
| ArrangingLayoutable CachedLineNumberMargin | 0.016 |
| ArrangingLayoutable TextView | 0.007 |
| ArrangingLayoutable LineDrawing | 0.005 |
| FindingResource - | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.25 | 1.06 | 1.35 | 100% | 0.22 | 0.91 | 2.39 | 40.7 → 63.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 19 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.33 | 1.69 | 4.18 | 100% | 0.35 | 1.44 | 2.58 | 95.6 → 92.4 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.57 | 2.88 | 10.81 | 99% | 0.40 | 2.41 | 2.78 | 309.3 → 301.2 | 9 | 1.1 / 4 |

