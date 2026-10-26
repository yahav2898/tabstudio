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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.95 | 2.65 | 11.06 | 99% | 0.55 | 2.11 | 4.87 | 113.4 → 114.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.20 | 2.96 | 12.18 | 99% | 0.78 | 2.21 | 5.02 | 152.0 → 149.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.37 | 3.62 | 15.82 | 99% | 1.17 | 2.56 | 5.05 | 187.7 → 172.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.06 | 2.86 | 12.21 | 99% | 0.69 | 2.11 | 4.97 | 131.2 → 128.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.85 | 2.53 | 10.68 | 99% | 0.67 | 1.87 | 4.59 | 107.8 → 101.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.90 | 2.56 | 13.58 | 99% | 0.65 | 1.96 | 4.70 | 133.2 → 125.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.06 | 3.61 | 11.19 | 100% | 0.79 | 2.54 | 5.25 | 134.2 → 125.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.95 | 2.65 | 11.32 | 99% | 0.69 | 2.05 | 4.75 | 132.4 → 124.6 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.90 | 2.37 | 11.77 | 99% | 0.56 | 1.88 | 4.52 | 110.6 → 105.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.09 | 4.09 | 12.17 | 99% | 0.75 | 2.70 | 5.04 | 136.1 → 129.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.90 | 2.45 | 11.79 | 99% | 0.63 | 1.96 | 4.23 | 135.8 → 127.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.59 | 2.08 | 8.63 | 99% | 0.55 | 1.64 | 3.63 | 72.0 → 68.7 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.277 |
| MeasuringLayoutable LineDrawing | 0.036 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.84 | 2.48 | 2.56 | 100% | 0.49 | 1.92 | 4.97 | 78.2 → 99.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 258 frames at 120 Hz; the text stood still on 0 of them, 256 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.98 | 2.72 | 11.59 | 99% | 0.64 | 2.15 | 5.17 | 132.0 → 128.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.44 | 3.20 | 16.31 | 98% | 0.81 | 2.42 | 4.87 | 344.7 → 337.7 | 8 | 1.1 / 4 |

