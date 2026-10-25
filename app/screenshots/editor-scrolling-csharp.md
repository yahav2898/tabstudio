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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 1.71 | 11.03 | 99% | 0.32 | 1.48 | 3.85 | 111.4 → 112.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 3.60 | 12.94 | 99% | 0.61 | 2.59 | 4.36 | 150.4 → 147.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.77 | 2.13 | 13.89 | 99% | 0.65 | 1.56 | 3.93 | 186.0 → 170.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.60 | 1.87 | 9.91 | 99% | 0.45 | 1.53 | 3.87 | 129.4 → 126.6 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 2.06 | 10.53 | 99% | 0.53 | 1.56 | 4.00 | 105.3 → 99.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 2.11 | 12.05 | 99% | 0.50 | 1.62 | 4.14 | 132.0 → 123.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.57 | 1.92 | 13.79 | 99% | 0.47 | 1.57 | 3.91 | 131.7 → 123.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 1.85 | 10.73 | 99% | 0.46 | 1.47 | 3.74 | 130.8 → 122.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 2.85 | 12.63 | 99% | 0.54 | 2.27 | 4.06 | 108.4 → 103.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.58 | 1.85 | 11.47 | 99% | 0.46 | 1.55 | 3.88 | 134.3 → 126.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.58 | 1.89 | 11.01 | 99% | 0.46 | 1.55 | 3.49 | 133.9 → 125.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.51 | 8.34 | 100% | 0.39 | 1.26 | 2.91 | 69.6 → 66.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.266 |
| ArrangingLayoutable NumberDrawing | 0.044 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable TextView | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.65 | 2.49 | 3.10 | 100% | 0.46 | 1.80 | 4.44 | 76.1 → 98.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 125 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.61 | 1.90 | 11.36 | 99% | 0.45 | 1.58 | 4.04 | 130.1 → 127.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.08 | 4.50 | 17.33 | 98% | 0.67 | 2.84 | 4.30 | 343.3 → 337.6 | 9 | 1.1 / 4 |

