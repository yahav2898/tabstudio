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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.71 | 2.96 | 17.45 | 99% | 0.47 | 2.57 | 4.04 | 86.5 → 87.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.85 | 3.17 | 16.80 | 99% | 0.65 | 2.64 | 4.06 | 124.2 → 121.9 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.88 | 4.57 | 17.25 | 99% | 1.94 | 2.67 | 4.23 | 159.9 → 144.2 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.79 | 3.21 | 18.67 | 99% | 0.60 | 2.67 | 4.14 | 103.9 → 100.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.66 | 2.96 | 9.74 | 100% | 0.57 | 2.50 | 3.87 | 81.3 → 73.9 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.69 | 3.02 | 16.81 | 99% | 0.57 | 2.55 | 3.84 | 106.5 → 98.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.76 | 3.19 | 14.10 | 99% | 0.64 | 2.64 | 4.03 | 106.4 → 98.0 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.83 | 3.40 | 17.64 | 99% | 0.70 | 2.76 | 4.11 | 105.7 → 97.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.66 | 2.94 | 12.61 | 99% | 0.48 | 2.51 | 3.74 | 83.8 → 78.3 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.72 | 3.02 | 16.14 | 99% | 0.60 | 2.53 | 3.91 | 109.1 → 100.9 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.69 | 3.05 | 13.53 | 99% | 0.61 | 2.56 | 3.55 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.48 | 2.67 | 11.98 | 100% | 0.71 | 2.28 | 2.92 | 46.5 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.285 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.73 | 3.20 | 5.17 | 100% | 0.52 | 2.55 | 4.48 | 50.2 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 223 frames at 120 Hz; the text stood still on 0 of them, 254 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.77 | 3.06 | 17.09 | 99% | 0.59 | 2.58 | 4.08 | 104.8 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 3.63 | 19.14 | 98% | 0.71 | 2.97 | 4.16 | 318.3 → 310.3 | 6 | 1.1 / 4 |

