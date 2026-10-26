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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.63 | 1.99 | 16.45 | 99% | 0.39 | 1.65 | 4.62 | 112.9 → 113.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 2.28 | 16.82 | 99% | 0.59 | 1.77 | 4.84 | 150.3 → 148.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.98 | 3.81 | 17.52 | 99% | 0.84 | 2.55 | 5.15 | 185.5 → 170.6 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.24 | 17.00 | 100% | 0.54 | 1.83 | 4.86 | 130.5 → 127.6 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 1.98 | 11.81 | 100% | 0.53 | 1.59 | 4.60 | 107.1 → 100.4 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.67 | 2.13 | 16.95 | 99% | 0.57 | 1.71 | 4.78 | 132.6 → 124.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 3.16 | 11.26 | 100% | 0.61 | 2.45 | 5.02 | 133.3 → 124.6 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.73 | 2.17 | 16.01 | 99% | 0.55 | 1.77 | 4.83 | 131.8 → 124.0 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.67 | 2.13 | 12.08 | 99% | 0.48 | 1.74 | 4.64 | 110.1 → 105.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.86 | 3.26 | 12.20 | 99% | 0.72 | 2.51 | 5.22 | 135.4 → 128.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.80 | 2.38 | 17.30 | 99% | 0.60 | 1.94 | 4.55 | 135.2 → 127.3 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.43 | 1.68 | 9.71 | 100% | 0.45 | 1.38 | 3.54 | 71.4 → 68.0 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.195 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.64 | 2.54 | 14.39 | 98% | 0.44 | 1.83 | 5.35 | 78.2 → 99.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 230 frames at 120 Hz; the text stood still on 0 of them, 261 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.28 | 14.11 | 99% | 0.53 | 1.80 | 4.93 | 130.8 → 128.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.11 | 2.65 | 19.78 | 99% | 0.63 | 2.12 | 4.95 | 342.1 → 335.5 | 5 | 1.1 / 4 |

