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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 2.69 | 10.18 | 99% | 0.43 | 1.64 | 3.69 | 112.9 → 114.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 2.18 | 9.85 | 99% | 0.57 | 1.60 | 3.99 | 151.0 → 148.2 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 1.90 | 14.71 | 99% | 0.60 | 1.43 | 3.56 | 186.2 → 170.7 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 1.66 | 9.65 | 99% | 0.44 | 1.33 | 3.36 | 130.0 → 127.4 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.42 | 1.44 | 8.28 | 100% | 0.40 | 1.15 | 3.11 | 106.5 → 99.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 2.56 | 11.98 | 99% | 0.51 | 1.43 | 3.47 | 132.3 → 125.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.62 | 2.60 | 9.84 | 99% | 0.49 | 1.54 | 3.75 | 133.1 → 124.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.49 | 1.57 | 9.57 | 99% | 0.40 | 1.24 | 3.17 | 131.6 → 123.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.46 | 1.48 | 8.29 | 100% | 0.34 | 1.22 | 3.08 | 109.5 → 104.4 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.49 | 1.56 | 8.96 | 100% | 0.40 | 1.23 | 3.26 | 134.8 → 127.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.48 | 1.55 | 9.25 | 99% | 0.40 | 1.24 | 2.96 | 134.9 → 126.8 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.45 | 2.48 | 9.53 | 100% | 0.48 | 1.34 | 2.87 | 71.0 → 67.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.173 |
| ArrangingLayoutable TextView | 0.009 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
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
| touch drag and flick (real touch input) | 46 | 0.41 | 1.45 | 1.55 | 100% | 0.34 | 1.19 | 3.29 | 76.3 → 99.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 2 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.49 | 1.56 | 8.85 | 100% | 0.41 | 1.24 | 3.28 | 130.6 → 127.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.92 | 3.20 | 16.06 | 98% | 0.58 | 2.70 | 3.73 | 343.1 → 336.9 | 9 | 1.1 / 4 |

