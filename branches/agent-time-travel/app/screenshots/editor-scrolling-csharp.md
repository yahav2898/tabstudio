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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.00 | 2.65 | 12.58 | 99% | 0.57 | 2.12 | 5.05 | 114.1 → 115.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.23 | 2.95 | 13.70 | 99% | 0.80 | 2.26 | 5.11 | 153.0 → 151.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.43 | 3.33 | 14.84 | 99% | 1.00 | 2.39 | 5.32 | 189.8 → 173.9 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.22 | 3.27 | 15.75 | 99% | 0.79 | 2.50 | 5.23 | 133.4 → 130.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.99 | 2.75 | 10.69 | 99% | 0.72 | 2.06 | 4.92 | 108.5 → 101.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.15 | 3.01 | 16.91 | 99% | 0.80 | 2.29 | 5.25 | 135.5 → 127.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.22 | 3.56 | 85.21 | 99% | 1.02 | 2.52 | 5.27 | 135.6 → 127.2 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.16 | 3.01 | 13.11 | 99% | 0.78 | 2.31 | 5.17 | 134.6 → 126.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.12 | 2.91 | 12.48 | 99% | 0.68 | 2.25 | 4.91 | 111.4 → 106.6 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.23 | 3.18 | 12.91 | 99% | 0.82 | 2.39 | 5.35 | 138.3 → 130.2 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.16 | 3.41 | 19.74 | 99% | 0.81 | 2.60 | 4.98 | 137.8 → 128.9 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.73 | 2.33 | 7.81 | 100% | 0.63 | 1.81 | 3.88 | 72.8 → 69.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.276 |
| AttachingStyle - | 0.084 |
| ArrangingLayoutable TextView | 0.023 |
| FindingResource - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.91 | 2.59 | 2.96 | 100% | 0.50 | 1.95 | 5.06 | 78.4 → 100.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 237 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.19 | 3.51 | 11.78 | 99% | 0.81 | 2.70 | 5.20 | 133.5 → 130.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.61 | 3.37 | 16.96 | 98% | 0.85 | 2.57 | 5.06 | 347.4 → 340.9 | 7 | 1.1 / 4 |

