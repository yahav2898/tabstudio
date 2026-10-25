# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 803 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.13.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 1.79 | 5.92 | 100% | 0.36 | 1.52 | 4.08 | 123.8 → 123.1 | 5 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.78 | 2.40 | 10.29 | 100% | 0.58 | 1.81 | 4.49 | 149.3 → 147.1 | 8 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.86 | 2.33 | 12.31 | 99% | 0.67 | 1.71 | 4.48 | 176.2 → 167.0 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.65 | 2.22 | 9.68 | 100% | 0.55 | 1.65 | 4.31 | 136.8 → 132.3 | 6 | 1.1 / 4 |
| without line numbers | 368 | 0.43 | 1.89 | 6.86 | 100% | 0.53 | 1.35 | 4.10 | 71.1 → 64.2 | 5 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 2.20 | 9.40 | 100% | 0.51 | 1.58 | 4.34 | 138.6 → 129.4 | 6 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.66 | 2.08 | 11.17 | 100% | 0.49 | 1.63 | 4.23 | 138.5 → 129.6 | 6 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 2.11 | 12.69 | 99% | 0.49 | 1.56 | 4.15 | 137.8 → 128.9 | 6 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 1.98 | 11.98 | 99% | 0.47 | 1.61 | 4.06 | 116.3 → 110.0 | 5 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 2.00 | 10.93 | 99% | 0.47 | 1.54 | 4.18 | 141.2 → 132.3 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.59 | 2.29 | 10.59 | 99% | 0.50 | 1.59 | 3.78 | 141.3 → 132.1 | 6 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.29 | 1.45 | 5.12 | 100% | 0.40 | 1.17 | 3.02 | 35.9 → 32.4 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 43.1

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.174 |
| ArrangingLayoutable LineDrawing | 0.017 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Grid | 0.001 |
| ArrangingLayoutable CachedLineNumberMargin | 0.001 |
| RaisingRoutedEvent ScrollViewer | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 1.68 | 1.93 | 100% | 0.30 | 1.40 | 3.87 | 102.4 → 116.8 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 53 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

