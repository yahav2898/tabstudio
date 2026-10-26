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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.94 | 2.65 | 9.07 | 99% | 0.57 | 2.14 | 4.88 | 113.5 → 114.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.14 | 3.76 | 11.62 | 99% | 0.80 | 2.67 | 5.30 | 152.4 → 149.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.16 | 2.96 | 15.40 | 99% | 0.88 | 2.12 | 4.93 | 187.7 → 171.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.96 | 2.60 | 11.40 | 99% | 0.62 | 2.03 | 4.78 | 131.2 → 128.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.87 | 2.66 | 11.36 | 99% | 0.67 | 1.98 | 4.88 | 107.8 → 101.0 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.92 | 2.93 | 12.41 | 99% | 0.71 | 2.05 | 4.85 | 134.0 → 125.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.94 | 2.54 | 10.76 | 99% | 0.69 | 2.00 | 4.72 | 133.4 → 125.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.84 | 2.39 | 11.04 | 99% | 0.62 | 1.90 | 4.53 | 132.6 → 124.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.85 | 2.73 | 13.75 | 99% | 0.58 | 2.05 | 4.64 | 110.5 → 105.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.99 | 2.74 | 16.65 | 99% | 0.65 | 2.11 | 4.95 | 136.6 → 128.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.91 | 2.45 | 13.52 | 99% | 0.65 | 1.92 | 4.20 | 136.0 → 128.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.57 | 2.01 | 7.52 | 100% | 0.53 | 1.59 | 3.44 | 71.6 → 68.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.246 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable TextView | 0.020 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.95 | 2.86 | 9.72 | 98% | 0.49 | 2.51 | 5.37 | 78.3 → 101.0 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 245 frames at 120 Hz; the text stood still on 0 of them, 263 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.98 | 2.56 | 11.77 | 99% | 0.69 | 2.04 | 4.81 | 132.0 → 129.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.42 | 3.10 | 16.79 | 98% | 0.74 | 2.39 | 4.75 | 344.8 → 337.6 | 8 | 1.1 / 4 |

