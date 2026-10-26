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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 1.74 | 7.65 | 100% | 0.30 | 1.42 | 3.36 | 112.0 → 112.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.55 | 1.76 | 11.75 | 99% | 0.43 | 1.40 | 3.30 | 149.3 → 147.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.61 | 1.89 | 12.86 | 99% | 0.51 | 1.41 | 3.38 | 184.5 → 168.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.47 | 1.66 | 9.88 | 99% | 0.39 | 1.38 | 3.25 | 129.3 → 126.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.39 | 1.53 | 7.50 | 100% | 0.38 | 1.26 | 3.08 | 106.0 → 99.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.58 | 3.78 | 14.74 | 99% | 1.44 | 2.67 | 3.58 | 131.8 → 124.3 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.49 | 1.66 | 10.20 | 99% | 0.40 | 1.37 | 3.31 | 131.4 → 123.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.47 | 1.61 | 12.81 | 99% | 0.40 | 1.34 | 3.27 | 130.9 → 122.9 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.45 | 1.57 | 7.56 | 100% | 0.34 | 1.33 | 3.09 | 109.0 → 104.2 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.48 | 1.65 | 9.49 | 99% | 0.39 | 1.37 | 3.26 | 133.9 → 126.5 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.49 | 1.78 | 8.81 | 100% | 0.41 | 1.45 | 3.00 | 133.9 → 126.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 2.42 | 8.92 | 100% | 0.42 | 1.32 | 2.64 | 71.0 → 67.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.135 |
| ArrangingLayoutable TextView | 0.009 |
| AttachingStyle - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.45 | 1.83 | 7.33 | 100% | 0.29 | 1.44 | 3.38 | 77.2 → 98.6 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 2 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.49 | 1.67 | 11.38 | 99% | 0.40 | 1.40 | 3.26 | 129.9 → 126.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.80 | 2.03 | 14.50 | 98% | 0.47 | 1.62 | 3.44 | 341.1 → 334.6 | 8 | 1.1 / 4 |

