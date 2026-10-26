# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 2.23 | 13.29 | 99% | 0.41 | 1.85 | 4.37 | 117.4 → 118.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.87 | 2.48 | 13.80 | 99% | 0.60 | 1.99 | 4.61 | 155.0 → 152.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.15 | 4.39 | 91.21 | 99% | 2.30 | 2.58 | 4.98 | 190.4 → 176.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 2.32 | 12.02 | 99% | 0.52 | 1.90 | 4.48 | 135.0 → 132.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 2.02 | 10.90 | 100% | 0.49 | 1.66 | 4.17 | 111.5 → 104.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.64 | 2.10 | 12.32 | 99% | 0.49 | 1.74 | 4.20 | 136.9 → 129.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 2.11 | 11.53 | 99% | 0.48 | 1.73 | 4.15 | 136.9 → 129.1 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.78 | 4.01 | 14.67 | 99% | 0.73 | 2.56 | 4.43 | 136.5 → 128.6 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 1.98 | 7.53 | 100% | 0.42 | 1.65 | 3.86 | 114.6 → 109.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.63 | 2.05 | 11.24 | 99% | 0.49 | 1.71 | 4.09 | 139.5 → 132.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.60 | 2.09 | 10.30 | 99% | 0.48 | 1.71 | 3.55 | 139.2 → 131.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 1.66 | 6.83 | 100% | 0.40 | 1.41 | 3.00 | 75.6 → 72.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.283 |
| ArrangingLayoutable ScrollingTextLayer | 0.068 |
| ArrangingLayoutable NumberDrawing | 0.047 |
| AttachingStyle - | 0.045 |
| ArrangingLayoutable CachedLineNumberMargin | 0.030 |
| ArrangingLayoutable TextView | 0.026 |
| ArrangingLayoutable LineDrawing | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.84 | 4.29 | 4.65 | 100% | 0.47 | 3.90 | 5.47 | 83.1 → 104.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 24 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.63 | 2.02 | 13.79 | 99% | 0.47 | 1.70 | 4.03 | 135.3 → 132.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.05 | 2.52 | 14.97 | 98% | 0.57 | 2.03 | 4.37 | 346.4 → 339.7 | 8 | 1.1 / 4 |

