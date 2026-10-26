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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.86 | 2.57 | 10.45 | 100% | 0.51 | 2.04 | 4.90 | 115.9 → 117.0 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 2.77 | 12.46 | 99% | 0.72 | 2.11 | 4.92 | 154.0 → 151.8 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.34 | 4.99 | 73.95 | 98% | 2.80 | 2.75 | 5.31 | 190.9 → 174.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.97 | 2.62 | 11.08 | 99% | 0.60 | 2.06 | 4.85 | 133.6 → 130.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.83 | 2.52 | 14.54 | 99% | 0.64 | 1.94 | 4.84 | 110.0 → 103.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.98 | 4.12 | 15.34 | 99% | 0.70 | 2.76 | 5.05 | 136.0 → 128.9 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.91 | 2.57 | 11.52 | 99% | 0.66 | 2.06 | 4.88 | 136.3 → 127.8 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.87 | 2.54 | 10.41 | 99% | 0.63 | 1.99 | 4.63 | 135.3 → 127.1 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.91 | 2.63 | 10.88 | 99% | 0.58 | 2.08 | 4.68 | 112.8 → 108.1 | 4 | 1.2 / 30 |
| without squiggles | 368 | 1.02 | 2.96 | 14.22 | 99% | 0.69 | 2.22 | 5.22 | 139.0 → 131.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.98 | 2.67 | 13.61 | 99% | 0.65 | 2.12 | 4.65 | 138.4 → 130.6 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.66 | 2.18 | 7.74 | 100% | 0.56 | 1.69 | 3.82 | 74.7 → 70.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.263 |
| ArrangingLayoutable NumberDrawing | 0.046 |
| ArrangingLayoutable TextView | 0.033 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.92 | 2.95 | 4.42 | 100% | 0.60 | 2.36 | 5.14 | 80.6 → 102.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 242 frames at 120 Hz; the text stood still on 0 of them, 263 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.95 | 2.54 | 11.51 | 99% | 0.62 | 2.04 | 4.94 | 134.2 → 131.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.48 | 5.19 | 15.64 | 98% | 0.80 | 4.43 | 5.07 | 347.4 → 341.3 | 9 | 1.1 / 4 |

