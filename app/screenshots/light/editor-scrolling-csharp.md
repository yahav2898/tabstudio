# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.85 | 2.71 | 100% | 0.32 | 1.60 | 3.75 | 84.4 → 85.2 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.61 | 2.06 | 41.84 | 100% | 0.52 | 1.67 | 3.86 | 121.3 → 121.8 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.71 | 2.13 | 3.41 | 100% | 0.75 | 1.65 | 3.95 | 156.8 → 141.7 | 0 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.54 | 1.97 | 41.62 | 100% | 0.45 | 1.63 | 3.82 | 101.6 → 98.8 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 2.54 | 4.02 | 100% | 0.58 | 2.19 | 3.60 | 79.7 → 73.1 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.55 | 2.57 | 25.26 | 99% | 0.52 | 2.03 | 4.07 | 104.3 → 96.0 | 1 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.53 | 2.00 | 4.93 | 100% | 0.50 | 1.64 | 4.34 | 104.2 → 96.0 | 0 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.52 | 1.98 | 2.74 | 100% | 0.46 | 1.61 | 3.75 | 103.5 → 95.3 | 0 | 1.2 / 29 |
| without coloring | 368 | 0.48 | 1.91 | 37.86 | 100% | 0.41 | 1.57 | 3.71 | 82.3 → 76.9 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.52 | 1.99 | 2.93 | 100% | 0.47 | 1.63 | 3.83 | 106.7 → 98.8 | 0 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.57 | 2.77 | 42.53 | 100% | 0.55 | 1.90 | 3.66 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 2.46 | 4.60 | 100% | 1.09 | 1.88 | 2.89 | 46.2 → 42.5 | 0 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.171 |
| AttachingStyle - | 0.016 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.45 | 1.87 | 2.45 | 100% | 0.32 | 1.52 | 4.08 | 49.2 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 53 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.54 | 1.99 | 2.93 | 100% | 0.47 | 1.63 | 3.86 | 102.6 → 99.3 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.92 | 3.17 | 68.87 | 99% | 1.08 | 2.56 | 3.88 | 307.6 → 299.8 | 3 | 1.1 / 4 |

