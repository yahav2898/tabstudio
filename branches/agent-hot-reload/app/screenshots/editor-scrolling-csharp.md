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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.72 | 2.22 | 9.97 | 99% | 0.43 | 1.79 | 4.25 | 113.3 → 114.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.15 | 3.37 | 16.78 | 99% | 0.82 | 2.67 | 5.07 | 151.7 → 149.9 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.30 | 2.97 | 13.62 | 99% | 0.93 | 2.16 | 4.95 | 187.7 → 171.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.06 | 2.84 | 13.10 | 99% | 0.75 | 2.13 | 4.91 | 131.4 → 128.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.87 | 2.54 | 11.75 | 99% | 0.71 | 1.92 | 4.67 | 108.0 → 101.2 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.06 | 3.25 | 11.41 | 99% | 0.75 | 2.31 | 4.99 | 134.3 → 125.3 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.83 | 2.98 | 12.49 | 99% | 0.58 | 2.48 | 4.59 | 133.5 → 125.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.89 | 3.04 | 12.50 | 99% | 0.66 | 2.55 | 4.56 | 132.9 → 124.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.05 | 3.39 | 11.69 | 99% | 0.69 | 2.79 | 4.82 | 111.0 → 106.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.10 | 3.43 | 13.11 | 99% | 0.72 | 2.74 | 5.10 | 137.4 → 128.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.99 | 3.03 | 12.10 | 99% | 0.69 | 2.50 | 4.43 | 136.0 → 128.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.72 | 2.56 | 12.72 | 99% | 0.62 | 1.93 | 3.83 | 72.6 → 69.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.325 |
| ArrangingLayoutable TextView | 0.023 |
| ArrangingLayoutable Track | 0.020 |
| ArrangingLayoutable RepeatButton | 0.014 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable ScrollingTextLayer | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.00 | 3.75 | 5.22 | 100% | 0.74 | 2.73 | 5.39 | 79.3 → 99.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 207 frames at 120 Hz; the text stood still on 0 of them, 241 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.02 | 3.23 | 11.35 | 99% | 0.68 | 2.67 | 5.05 | 133.0 → 129.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.32 | 3.43 | 18.30 | 98% | 0.75 | 2.90 | 4.73 | 344.7 → 337.8 | 8 | 1.1 / 4 |

