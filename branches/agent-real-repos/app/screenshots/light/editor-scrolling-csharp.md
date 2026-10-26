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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.99 | 2.62 | 11.31 | 100% | 0.56 | 2.10 | 5.08 | 117.7 → 118.8 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.24 | 4.79 | 14.20 | 99% | 0.92 | 4.09 | 5.29 | 155.9 → 154.3 | 6 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.24 | 3.06 | 16.60 | 99% | 0.92 | 2.26 | 5.15 | 191.0 → 175.2 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.08 | 2.93 | 12.74 | 99% | 0.69 | 2.25 | 5.18 | 135.5 → 133.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.92 | 2.63 | 11.62 | 99% | 0.69 | 2.04 | 4.85 | 111.9 → 105.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.15 | 4.05 | 14.72 | 99% | 0.87 | 2.76 | 5.65 | 138.3 → 130.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.08 | 2.94 | 12.00 | 99% | 0.74 | 2.30 | 5.18 | 137.8 → 130.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.06 | 3.00 | 12.77 | 99% | 0.77 | 2.28 | 5.06 | 136.9 → 129.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.99 | 2.80 | 10.85 | 99% | 0.62 | 2.21 | 4.88 | 114.8 → 110.1 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.20 | 4.48 | 14.80 | 99% | 0.77 | 2.89 | 5.31 | 141.4 → 133.3 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.06 | 2.83 | 15.06 | 99% | 0.72 | 2.20 | 4.55 | 139.7 → 132.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.75 | 2.39 | 8.81 | 100% | 0.63 | 1.88 | 3.88 | 77.0 → 73.4 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.332 |
| AttachingStyle - | 0.096 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.94 | 2.78 | 3.00 | 100% | 0.51 | 2.11 | 5.47 | 82.7 → 104.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2246 ms over 212 frames at 120 Hz; the text stood still on 0 of them, 245 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.09 | 3.01 | 13.40 | 99% | 0.72 | 2.31 | 5.17 | 136.7 → 133.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.58 | 3.32 | 19.09 | 98% | 0.84 | 2.55 | 5.11 | 348.3 → 341.8 | 7 | 1.1 / 4 |

