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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.14 | 11.13 | 99% | 0.43 | 1.81 | 4.38 | 113.5 → 114.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.90 | 2.47 | 12.02 | 99% | 0.66 | 1.92 | 4.54 | 151.6 → 149.3 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.01 | 4.20 | 13.93 | 99% | 1.12 | 2.39 | 4.56 | 187.0 → 171.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 2.18 | 11.29 | 100% | 0.52 | 1.69 | 4.38 | 131.3 → 128.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.04 | 10.77 | 100% | 0.54 | 1.64 | 4.10 | 107.5 → 100.9 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.85 | 2.43 | 12.97 | 99% | 0.65 | 1.88 | 4.63 | 133.4 → 125.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.99 | 3.29 | 11.07 | 99% | 0.80 | 2.60 | 4.79 | 134.0 → 126.0 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.84 | 2.39 | 9.21 | 100% | 0.62 | 1.85 | 4.53 | 132.7 → 124.7 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.77 | 2.30 | 10.06 | 99% | 0.52 | 1.84 | 4.38 | 110.5 → 105.7 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.93 | 3.53 | 13.22 | 99% | 0.73 | 2.48 | 4.73 | 136.0 → 129.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 2.44 | 9.64 | 99% | 0.60 | 1.89 | 4.03 | 136.0 → 128.0 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.55 | 1.94 | 8.48 | 99% | 0.52 | 1.56 | 3.39 | 71.6 → 68.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.012 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 1.88 | 1.92 | 100% | 0.45 | 1.54 | 4.19 | 78.2 → 99.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 260 frames at 120 Hz; the text stood still on 0 of them, 138 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 2.29 | 8.92 | 99% | 0.58 | 1.81 | 4.32 | 131.7 → 128.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.32 | 3.00 | 17.38 | 98% | 0.72 | 2.31 | 4.63 | 343.9 → 337.7 | 8 | 1.1 / 4 |

