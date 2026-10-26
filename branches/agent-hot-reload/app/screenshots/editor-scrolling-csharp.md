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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.95 | 2.57 | 11.89 | 99% | 0.55 | 2.11 | 5.08 | 113.9 → 115.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.23 | 3.48 | 12.27 | 99% | 0.83 | 2.62 | 5.20 | 152.9 → 151.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.29 | 2.98 | 15.38 | 99% | 0.87 | 2.17 | 5.18 | 189.5 → 173.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.03 | 2.68 | 14.06 | 99% | 0.70 | 2.05 | 4.94 | 132.3 → 129.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.87 | 2.48 | 13.53 | 99% | 0.66 | 1.91 | 4.86 | 108.8 → 101.6 | 4 | 1.2 / 30 |
| without the folding margin | 368 | 1.01 | 4.29 | 14.60 | 99% | 0.74 | 2.89 | 5.03 | 134.9 → 126.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.00 | 2.67 | 11.86 | 100% | 0.68 | 2.03 | 4.91 | 134.3 → 126.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.05 | 2.75 | 11.89 | 99% | 0.73 | 2.12 | 4.94 | 133.7 → 125.9 | 2 | 1.2 / 30 |
| without coloring | 368 | 1.00 | 3.10 | 12.59 | 99% | 0.62 | 2.49 | 4.82 | 111.2 → 106.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.08 | 2.94 | 12.41 | 99% | 0.77 | 2.17 | 5.07 | 137.8 → 129.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.02 | 2.54 | 16.67 | 99% | 0.71 | 2.02 | 4.34 | 136.9 → 128.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 2.22 | 8.33 | 100% | 0.62 | 1.68 | 3.84 | 72.8 → 69.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.296 |
| AttachingStyle - | 0.045 |
| ArrangingLayoutable CachedLineNumberMargin | 0.031 |
| ArrangingLayoutable ScrollingTextLayer | 0.029 |
| ArrangingLayoutable TextView | 0.022 |
| ArrangingLayoutable LineDrawing | 0.021 |
| ArrangingLayoutable NumberDrawing | 0.017 |
| FindingResource - | 0.014 |
| ArrangingLayoutable Rectangle | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.92 | 2.94 | 3.19 | 100% | 0.50 | 2.36 | 5.63 | 78.4 → 100.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 230 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.05 | 2.84 | 12.86 | 99% | 0.73 | 2.20 | 5.02 | 132.9 → 130.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.59 | 3.60 | 18.43 | 98% | 0.86 | 2.94 | 5.00 | 347.3 → 340.9 | 9 | 1.1 / 4 |

