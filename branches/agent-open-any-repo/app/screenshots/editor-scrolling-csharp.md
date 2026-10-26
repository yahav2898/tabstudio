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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 2.57 | 10.73 | 99% | 0.54 | 2.09 | 5.12 | 114.4 → 115.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.10 | 2.89 | 13.09 | 99% | 0.75 | 2.21 | 5.09 | 153.1 → 150.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.47 | 5.13 | 14.20 | 99% | 2.51 | 3.36 | 5.47 | 189.9 → 173.8 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.00 | 2.73 | 16.30 | 99% | 0.65 | 2.12 | 5.06 | 132.5 → 129.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 2.52 | 11.75 | 99% | 0.66 | 1.94 | 4.92 | 108.8 → 101.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.12 | 3.64 | 13.99 | 99% | 0.77 | 2.84 | 5.33 | 135.0 → 127.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.01 | 3.32 | 14.63 | 99% | 0.73 | 2.77 | 5.23 | 135.8 → 126.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.05 | 3.41 | 14.62 | 99% | 0.72 | 2.80 | 5.15 | 135.0 → 126.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.87 | 3.36 | 11.72 | 99% | 0.64 | 2.74 | 4.94 | 111.1 → 106.8 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.06 | 3.30 | 13.71 | 99% | 0.69 | 2.77 | 5.35 | 138.5 → 129.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.79 | 2.86 | 13.50 | 99% | 0.57 | 2.45 | 4.39 | 136.8 → 128.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 2.68 | 7.31 | 100% | 0.53 | 2.28 | 3.79 | 72.8 → 69.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.367 |
| AttachingStyle - | 0.045 |
| ArrangingLayoutable TextView | 0.032 |
| FindingResource - | 0.024 |
| ArrangingLayoutable CachedLineNumberMargin | 0.022 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| RaisingRoutedEvent ScrollBar | 0.015 |
| ArrangingLayoutable Thumb | 0.012 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.84 | 3.19 | 3.29 | 100% | 0.51 | 2.56 | 4.96 | 78.4 → 100.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 230 frames at 120 Hz; the text stood still on 0 of them, 263 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.05 | 3.35 | 13.02 | 99% | 0.67 | 2.76 | 5.31 | 133.9 → 130.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.74 | 6.02 | 74.14 | 97% | 1.21 | 4.86 | 5.55 | 347.6 → 340.9 | 9 | 1.1 / 4 |

