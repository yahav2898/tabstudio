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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.57 | 2.27 | 78.35 | 100% | 0.39 | 1.92 | 3.08 | 112.7 → 113.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.58 | 2.12 | 12.42 | 99% | 0.48 | 1.79 | 2.84 | 150.3 → 148.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.63 | 2.17 | 12.84 | 99% | 0.51 | 1.83 | 2.81 | 185.8 → 170.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 2.18 | 11.02 | 99% | 0.41 | 1.86 | 2.83 | 130.1 → 127.2 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.46 | 2.05 | 9.56 | 100% | 0.43 | 1.77 | 2.64 | 106.4 → 99.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 2.77 | 13.43 | 99% | 0.53 | 2.06 | 2.95 | 132.4 → 124.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.68 | 3.10 | 14.22 | 99% | 0.78 | 2.45 | 3.54 | 133.2 → 124.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.51 | 2.10 | 9.48 | 99% | 0.43 | 1.79 | 2.68 | 131.6 → 123.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 1.99 | 9.68 | 100% | 0.34 | 1.73 | 2.68 | 109.4 → 104.4 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.57 | 2.08 | 10.32 | 99% | 0.42 | 1.76 | 2.76 | 134.8 → 127.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.56 | 2.08 | 9.88 | 99% | 0.43 | 1.79 | 2.43 | 134.9 → 126.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.67 | 5.78 | 100% | 0.40 | 1.57 | 1.94 | 70.4 → 67.1 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.306 |
| AttachingStyle - | 0.032 |
| ArrangingLayoutable TextView | 0.022 |
| ArrangingLayoutable LineDrawing | 0.019 |
| FindingResource - | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.69 | 2.83 | 12.91 | 98% | 0.41 | 1.88 | 3.33 | 77.3 → 99.0 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 64 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.35 | 1.76 | 15.32 | 99% | 0.30 | 1.58 | 2.45 | 130.2 → 127.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.59 | 2.09 | 13.25 | 98% | 0.38 | 1.81 | 2.50 | 342.7 → 336.1 | 8 | 1.1 / 4 |

