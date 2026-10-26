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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 1.92 | 10.92 | 99% | 0.33 | 1.65 | 4.10 | 117.6 → 119.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.35 | 13.81 | 99% | 0.58 | 1.86 | 4.56 | 155.8 → 153.4 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.96 | 3.27 | 14.47 | 99% | 0.84 | 2.52 | 4.74 | 191.4 → 176.5 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 2.72 | 12.83 | 99% | 0.54 | 1.99 | 4.68 | 135.7 → 132.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 2.06 | 15.59 | 99% | 0.52 | 1.70 | 4.50 | 112.2 → 105.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 2.06 | 13.28 | 99% | 0.50 | 1.68 | 4.45 | 138.1 → 129.7 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.70 | 2.15 | 12.92 | 99% | 0.50 | 1.78 | 4.70 | 137.6 → 129.7 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 4.01 | 17.63 | 99% | 0.81 | 2.51 | 4.80 | 137.5 → 129.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.79 | 2.47 | 11.72 | 99% | 0.52 | 1.96 | 4.61 | 115.1 → 110.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 2.25 | 14.52 | 99% | 0.53 | 1.82 | 4.58 | 140.2 → 132.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.69 | 2.15 | 12.28 | 99% | 0.52 | 1.76 | 3.96 | 140.5 → 132.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 1.95 | 11.42 | 99% | 0.48 | 1.57 | 3.45 | 76.3 → 72.9 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.328 |
| ArrangingLayoutable Thumb | 0.046 |
| AttachingStyle - | 0.023 |
| ArrangingLayoutable CachedLineNumberMargin | 0.021 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| ArrangingLayoutable Track | 0.017 |
| ArrangingLayoutable TextView | 0.016 |
| ArrangingLayoutable ScrollContentPresenter | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.55 | 1.90 | 11.40 | 98% | 0.38 | 1.68 | 4.85 | 82.8 → 104.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 44 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.69 | 2.14 | 12.75 | 99% | 0.54 | 1.75 | 4.40 | 136.0 → 133.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.19 | 2.83 | 18.14 | 98% | 0.66 | 2.30 | 4.80 | 348.3 → 342.0 | 8 | 1.1 / 4 |

