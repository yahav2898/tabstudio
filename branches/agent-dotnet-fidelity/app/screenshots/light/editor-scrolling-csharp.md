# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.80 | 3.35 | 13.42 | 100% | 0.51 | 2.95 | 4.89 | 86.1 → 87.0 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.04 | 3.67 | 17.25 | 99% | 0.77 | 3.03 | 4.91 | 123.4 → 121.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.15 | 3.77 | 18.55 | 99% | 0.93 | 3.01 | 5.04 | 158.6 → 143.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.98 | 4.17 | 14.39 | 99% | 1.26 | 3.05 | 5.02 | 103.4 → 100.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.29 | 10.02 | 100% | 0.68 | 2.77 | 4.68 | 80.6 → 73.4 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.97 | 3.58 | 15.12 | 99% | 0.73 | 3.00 | 4.93 | 105.9 → 97.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.07 | 4.01 | 16.71 | 99% | 0.99 | 3.12 | 5.16 | 105.8 → 97.6 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 3.70 | 13.36 | 99% | 0.72 | 3.02 | 4.90 | 105.1 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.82 | 3.34 | 13.77 | 99% | 0.56 | 2.85 | 4.60 | 83.4 → 78.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.92 | 3.49 | 12.96 | 99% | 0.66 | 2.92 | 4.94 | 108.4 → 100.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 3.77 | 13.17 | 99% | 1.10 | 2.85 | 4.09 | 108.5 → 100.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.84 | 9.70 | 100% | 0.56 | 2.44 | 3.28 | 46.0 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.223 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 3.35 | 3.65 | 100% | 0.50 | 2.67 | 4.96 | 50.1 → 72.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 230 frames at 120 Hz; the text stood still on 0 of them, 263 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 4.15 | 77.51 | 99% | 0.98 | 3.10 | 5.19 | 104.2 → 101.0 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.37 | 4.02 | 19.34 | 98% | 0.76 | 3.35 | 4.99 | 316.6 → 308.6 | 7 | 1.1 / 4 |

