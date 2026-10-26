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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.54 | 1.63 | 1.86 | 100% | 0.34 | 1.35 | 3.72 | 112.7 → 113.8 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 1.83 | 43.84 | 100% | 0.55 | 1.38 | 3.84 | 151.1 → 148.7 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 3.67 | 8.96 | 100% | 2.30 | 2.53 | 3.99 | 186.7 → 171.2 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.63 | 1.77 | 3.66 | 100% | 0.51 | 1.35 | 4.02 | 130.7 → 127.7 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.51 | 1.57 | 38.38 | 100% | 0.48 | 1.21 | 3.58 | 106.7 → 99.9 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 1.66 | 2.01 | 100% | 0.48 | 1.30 | 3.73 | 132.6 → 124.7 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.63 | 1.70 | 40.15 | 100% | 0.50 | 1.33 | 3.82 | 133.0 → 124.8 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.68 | 3.24 | 5.67 | 100% | 2.28 | 2.47 | 3.81 | 132.2 → 124.7 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.58 | 1.68 | 21.81 | 100% | 0.44 | 1.33 | 3.68 | 109.8 → 105.0 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 1.73 | 39.23 | 100% | 0.49 | 1.37 | 3.88 | 135.5 → 127.8 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.62 | 1.65 | 2.60 | 100% | 0.49 | 1.32 | 3.50 | 135.5 → 127.1 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.41 | 1.67 | 100% | 0.42 | 1.10 | 2.77 | 71.1 → 67.5 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.262 |
| AttachingStyle - | 0.192 |
| ArrangingLayoutable CachedLineNumberMargin | 0.018 |
| RaisingRoutedEvent ScrollViewer | 0.011 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 1.48 | 6.75 | 7.99 | 100% | 2.19 | 5.61 | 5.61 | 80.4 → 100.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 2 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 1.70 | 2.42 | 100% | 0.49 | 1.35 | 3.90 | 131.4 → 128.3 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.99 | 2.09 | 48.27 | 99% | 0.57 | 1.62 | 3.90 | 343.6 → 337.1 | 2 | 1.1 / 4 |

