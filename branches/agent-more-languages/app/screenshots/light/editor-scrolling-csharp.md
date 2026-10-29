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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.94 | 3.60 | 17.06 | 99% | 0.60 | 3.02 | 5.09 | 86.8 → 87.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.97 | 3.62 | 16.37 | 100% | 0.79 | 2.96 | 4.74 | 124.5 → 122.3 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 3.73 | 17.15 | 99% | 0.96 | 2.94 | 4.80 | 160.3 → 144.4 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.95 | 3.67 | 15.93 | 99% | 0.74 | 2.94 | 5.07 | 104.2 → 101.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.79 | 3.39 | 13.29 | 100% | 0.73 | 2.78 | 4.80 | 81.1 → 73.8 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.87 | 3.45 | 13.25 | 99% | 0.67 | 2.85 | 4.80 | 106.8 → 98.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.85 | 3.46 | 20.71 | 99% | 0.69 | 2.88 | 4.75 | 106.7 → 98.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 4.82 | 82.88 | 99% | 1.57 | 3.09 | 5.08 | 106.0 → 97.7 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 3.33 | 12.46 | 99% | 0.56 | 2.81 | 4.46 | 83.9 → 78.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 3.24 | 13.91 | 100% | 0.61 | 2.79 | 4.53 | 109.2 → 101.2 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.76 | 3.32 | 20.13 | 99% | 0.65 | 2.82 | 4.49 | 109.4 → 101.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.56 | 3.09 | 11.50 | 100% | 1.37 | 2.60 | 3.83 | 46.1 → 42.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.232 |
| ArrangingLayoutable TextView | 0.017 |
| AttachingStyle - | 0.013 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.64 | 3.24 | 3.24 | 100% | 0.50 | 2.69 | 4.70 | 50.2 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 219 frames at 120 Hz; the text stood still on 0 of them, 251 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.27 | 15.72 | 99% | 0.63 | 2.80 | 4.64 | 105.0 → 101.8 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.57 | 6.08 | 85.53 | 96% | 2.57 | 5.33 | 5.13 | 318.7 → 310.7 | 8 | 1.1 / 4 |

