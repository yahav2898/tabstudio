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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.42 | 1.49 | 8.98 | 99% | 0.29 | 1.26 | 2.57 | 117.7 → 119.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.50 | 1.85 | 13.79 | 99% | 0.42 | 1.37 | 2.69 | 155.8 → 153.9 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.69 | 3.56 | 10.17 | 99% | 2.42 | 2.54 | 3.41 | 192.2 → 177.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.44 | 1.55 | 9.54 | 99% | 0.32 | 1.32 | 2.68 | 135.8 → 132.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.34 | 1.39 | 9.36 | 99% | 0.32 | 1.12 | 2.40 | 112.3 → 105.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.37 | 1.40 | 13.12 | 99% | 0.32 | 1.17 | 2.43 | 137.6 → 129.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.36 | 1.34 | 9.51 | 99% | 0.32 | 1.13 | 2.37 | 137.4 → 129.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.36 | 1.36 | 9.73 | 99% | 0.35 | 1.17 | 2.51 | 136.8 → 129.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.39 | 1.44 | 7.73 | 100% | 0.31 | 1.18 | 2.39 | 114.9 → 110.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.50 | 2.75 | 13.89 | 99% | 0.45 | 2.29 | 2.74 | 140.6 → 133.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.49 | 2.58 | 9.70 | 100% | 0.39 | 2.30 | 2.44 | 140.9 → 132.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.22 | 1.16 | 6.82 | 100% | 0.29 | 0.98 | 1.65 | 76.3 → 72.8 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.151 |
| AttachingStyle - | 0.042 |
| ArrangingLayoutable TextView | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.005 |
| ArrangingLayoutable CachedLineNumberMargin | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| RaisingRoutedEvent ScrollBar | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.34 | 1.37 | 1.50 | 100% | 0.26 | 1.10 | 2.38 | 82.6 → 103.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2236 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 3 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.39 | 1.48 | 8.52 | 99% | 0.36 | 1.21 | 3.47 | 136.5 → 133.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.64 | 1.74 | 12.79 | 98% | 0.40 | 1.42 | 2.58 | 348.6 → 342.2 | 8 | 1.1 / 4 |

