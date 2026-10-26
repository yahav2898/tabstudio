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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.98 | 3.76 | 12.36 | 99% | 0.57 | 2.79 | 5.18 | 120.6 → 122.5 | 4 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.01 | 2.75 | 14.76 | 99% | 0.69 | 2.15 | 4.84 | 158.3 → 155.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.07 | 2.73 | 13.79 | 99% | 0.78 | 2.04 | 4.81 | 193.9 → 178.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.84 | 2.46 | 12.22 | 99% | 0.59 | 1.93 | 4.64 | 137.9 → 134.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.76 | 2.73 | 11.72 | 99% | 0.64 | 2.01 | 4.74 | 114.8 → 108.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.83 | 2.66 | 16.06 | 99% | 0.63 | 2.04 | 4.70 | 140.6 → 132.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 2.40 | 12.70 | 99% | 0.58 | 1.91 | 4.66 | 140.0 → 132.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.85 | 2.40 | 12.96 | 99% | 0.59 | 1.93 | 4.62 | 139.3 → 131.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.82 | 2.47 | 11.30 | 99% | 0.55 | 1.94 | 4.47 | 117.4 → 112.5 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.93 | 3.43 | 13.90 | 99% | 0.73 | 2.51 | 4.99 | 143.6 → 135.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.83 | 2.36 | 12.80 | 99% | 0.58 | 1.94 | 4.07 | 142.7 → 134.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.50 | 1.97 | 11.94 | 100% | 0.50 | 1.60 | 3.36 | 78.9 → 75.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.223 |
| ArrangingLayoutable TextView | 0.018 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.72 | 2.25 | 2.60 | 100% | 0.45 | 1.85 | 5.00 | 85.1 → 106.8 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 256 frames at 120 Hz; the text stood still on 0 of them, 236 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.87 | 2.56 | 80.70 | 99% | 0.60 | 2.04 | 4.72 | 138.4 → 135.7 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.28 | 3.05 | 17.49 | 98% | 0.69 | 2.43 | 4.73 | 350.9 → 344.5 | 8 | 1.1 / 4 |

