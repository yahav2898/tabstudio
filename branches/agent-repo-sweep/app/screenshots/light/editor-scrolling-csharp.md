# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.46 | 2.44 | 9.38 | 100% | 0.31 | 2.19 | 3.27 | 86.0 → 86.9 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.59 | 2.64 | 14.68 | 99% | 0.53 | 2.26 | 3.42 | 123.4 → 121.1 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.64 | 2.74 | 14.92 | 99% | 0.65 | 2.27 | 3.49 | 158.5 → 142.9 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 3.04 | 14.75 | 99% | 1.62 | 2.47 | 3.66 | 103.2 → 100.4 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 2.48 | 13.13 | 100% | 0.46 | 2.17 | 3.48 | 80.5 → 73.3 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.51 | 2.78 | 13.50 | 99% | 0.47 | 2.28 | 3.72 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.52 | 2.61 | 15.93 | 99% | 0.48 | 2.30 | 3.48 | 105.7 → 97.4 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.51 | 2.55 | 12.61 | 100% | 0.43 | 2.24 | 3.34 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 2.74 | 9.33 | 100% | 0.48 | 2.38 | 3.58 | 83.4 → 78.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.52 | 2.64 | 13.65 | 99% | 0.47 | 2.28 | 3.44 | 108.3 → 100.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.49 | 2.63 | 14.46 | 99% | 0.48 | 2.27 | 3.09 | 108.4 → 100.2 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.49 | 8.97 | 100% | 1.30 | 2.14 | 2.56 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.175 |
| ArrangingLayoutable TextView | 0.049 |
| RaisingRoutedEvent ScrollViewer | 0.047 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.52 | 2.65 | 2.78 | 100% | 0.39 | 2.23 | 3.65 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 38 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 3.35 | 15.38 | 99% | 0.70 | 2.52 | 3.82 | 104.2 → 100.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 3.39 | 17.93 | 98% | 0.60 | 2.86 | 3.93 | 316.6 → 308.6 | 7 | 1.1 / 4 |

