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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 3.47 | 32.11 | 99% | 0.55 | 2.91 | 4.80 | 84.5 → 85.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 3.53 | 26.15 | 100% | 0.71 | 2.90 | 4.73 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.05 | 3.71 | 17.78 | 99% | 1.08 | 2.90 | 4.80 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.76 | 3.27 | 16.45 | 99% | 0.65 | 2.72 | 4.52 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.69 | 3.23 | 16.82 | 99% | 0.81 | 2.63 | 4.51 | 79.7 → 73.0 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.78 | 3.36 | 14.68 | 99% | 0.64 | 2.76 | 4.77 | 104.4 → 96.0 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.76 | 3.30 | 17.66 | 99% | 0.65 | 2.74 | 4.50 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.72 | 3.28 | 13.83 | 100% | 0.63 | 2.72 | 4.35 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.72 | 3.14 | 13.91 | 99% | 0.56 | 2.68 | 4.47 | 82.3 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.90 | 3.69 | 11.88 | 99% | 1.48 | 2.89 | 4.84 | 106.7 → 98.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.75 | 3.18 | 14.74 | 99% | 0.64 | 2.66 | 3.88 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.47 | 2.78 | 4.19 | 100% | 0.57 | 2.38 | 3.19 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.281 |
| AttachingStyle - | 0.019 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.014 |
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
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.61 | 3.28 | 3.72 | 100% | 0.55 | 2.74 | 5.01 | 49.2 → 73.0 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 183 frames at 120 Hz; the text stood still on 0 of them, 217 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 3.33 | 22.39 | 99% | 0.64 | 2.78 | 4.54 | 102.7 → 99.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.27 | 3.77 | 19.38 | 98% | 0.74 | 3.14 | 4.54 | 307.7 → 299.9 | 7 | 1.1 / 4 |

