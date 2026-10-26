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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.55 | 1.49 | 40.69 | 100% | 0.36 | 1.23 | 3.56 | 112.8 → 113.9 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.72 | 2.66 | 44.29 | 100% | 0.60 | 2.08 | 3.61 | 151.4 → 149.6 | 2 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.77 | 3.69 | 82.28 | 100% | 2.44 | 2.49 | 3.98 | 188.4 → 171.7 | 0 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.58 | 1.61 | 35.69 | 100% | 0.47 | 1.29 | 3.60 | 130.9 → 127.9 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 1.47 | 1.82 | 100% | 0.46 | 1.15 | 3.40 | 107.3 → 100.4 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.58 | 1.55 | 36.46 | 100% | 0.47 | 1.21 | 3.54 | 133.1 → 125.2 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.60 | 1.69 | 40.05 | 100% | 0.50 | 1.29 | 3.58 | 133.1 → 125.2 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 3.11 | 4.17 | 100% | 0.70 | 2.49 | 3.57 | 132.7 → 124.9 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.54 | 1.55 | 2.87 | 100% | 0.39 | 1.23 | 3.43 | 109.8 → 104.9 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.60 | 1.57 | 2.02 | 100% | 0.46 | 1.25 | 3.55 | 135.7 → 127.9 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.58 | 1.63 | 2.57 | 100% | 0.47 | 1.25 | 3.12 | 135.8 → 127.5 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.28 | 1.63 | 100% | 0.39 | 1.01 | 2.62 | 71.0 → 67.5 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.168 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| FindingResource - | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.50 | 2.79 | 4.17 | 100% | 0.48 | 1.49 | 3.66 | 77.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 44 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.60 | 1.62 | 25.50 | 100% | 0.48 | 1.25 | 3.59 | 131.4 → 128.7 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.95 | 2.04 | 51.16 | 99% | 0.54 | 1.59 | 3.67 | 345.3 → 338.8 | 2 | 1.1 / 4 |

