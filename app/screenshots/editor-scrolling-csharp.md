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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.49 | 1.92 | 2.89 | 100% | 0.32 | 1.64 | 3.72 | 84.8 → 85.6 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.62 | 2.11 | 50.50 | 100% | 0.52 | 1.65 | 3.91 | 122.0 → 122.6 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.72 | 2.15 | 3.40 | 100% | 0.75 | 1.67 | 3.95 | 158.1 → 143.0 | 0 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.57 | 2.06 | 41.76 | 100% | 0.49 | 1.65 | 3.86 | 102.2 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 3.60 | 85.99 | 100% | 2.31 | 2.19 | 3.73 | 80.5 → 73.6 | 0 | 1.2 / 29 |
| without the folding margin | 368 | 0.53 | 2.02 | 3.61 | 100% | 0.51 | 1.63 | 4.03 | 105.0 → 96.6 | 0 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.55 | 1.94 | 30.96 | 100% | 0.48 | 1.60 | 3.85 | 104.9 → 96.6 | 1 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.54 | 2.01 | 2.28 | 100% | 0.50 | 1.64 | 3.81 | 104.2 → 95.9 | 0 | 1.2 / 29 |
| without coloring | 368 | 0.50 | 1.97 | 38.85 | 100% | 0.40 | 1.63 | 3.81 | 82.7 → 77.2 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.56 | 2.03 | 3.49 | 100% | 0.49 | 1.64 | 3.86 | 107.4 → 99.4 | 0 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.60 | 2.95 | 50.51 | 100% | 1.16 | 2.28 | 3.69 | 107.5 → 99.3 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.37 | 2.39 | 4.30 | 100% | 1.09 | 1.45 | 2.85 | 46.5 → 42.8 | 0 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.173 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.46 | 1.91 | 2.08 | 100% | 0.32 | 1.56 | 3.94 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 58 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.55 | 2.02 | 2.97 | 100% | 0.46 | 1.66 | 3.81 | 103.2 → 99.8 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.91 | 4.19 | 69.42 | 99% | 2.30 | 2.69 | 3.91 | 308.8 → 301.0 | 3 | 1.1 / 4 |

