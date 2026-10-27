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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.35 | 1.78 | 9.97 | 99% | 0.28 | 1.61 | 2.92 | 117.5 → 119.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.63 | 2.89 | 12.88 | 99% | 0.50 | 2.45 | 3.02 | 156.5 → 154.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.48 | 1.88 | 12.98 | 99% | 0.44 | 1.65 | 2.55 | 191.9 → 176.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.39 | 1.73 | 10.48 | 99% | 0.31 | 1.57 | 2.47 | 135.6 → 132.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.31 | 1.70 | 8.60 | 99% | 0.32 | 1.53 | 2.20 | 112.1 → 105.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.36 | 1.75 | 10.18 | 99% | 0.32 | 1.56 | 2.37 | 137.8 → 130.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.35 | 1.69 | 9.66 | 99% | 0.30 | 1.53 | 2.33 | 137.9 → 129.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.35 | 1.74 | 10.28 | 99% | 0.31 | 1.56 | 2.32 | 137.0 → 128.9 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 2.81 | 61.60 | 99% | 0.38 | 2.41 | 2.47 | 115.7 → 110.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.41 | 2.13 | 9.13 | 99% | 0.36 | 1.73 | 2.73 | 140.9 → 132.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.35 | 1.50 | 9.10 | 99% | 0.30 | 1.24 | 1.98 | 140.3 → 132.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.22 | 1.52 | 6.86 | 100% | 0.26 | 1.41 | 1.64 | 76.5 → 73.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.135 |
| ArrangingLayoutable TextView | 0.037 |
| AttachingStyle - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable LineDrawing | 0.005 |
| FindingResource - | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.31 | 1.82 | 7.08 | 100% | 0.22 | 1.61 | 2.44 | 82.6 → 104.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 1 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.44 | 2.37 | 12.81 | 100% | 0.38 | 1.80 | 2.52 | 136.2 → 134.0 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.74 | 3.97 | 68.83 | 98% | 0.56 | 3.49 | 2.95 | 350.1 → 342.2 | 7 | 1.1 / 4 |

