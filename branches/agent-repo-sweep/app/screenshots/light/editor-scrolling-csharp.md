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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.50 | 2.80 | 10.00 | 99% | 0.32 | 2.52 | 3.93 | 86.7 → 87.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.19 | 12.45 | 100% | 0.63 | 2.68 | 4.32 | 124.5 → 122.3 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 4.95 | 86.67 | 99% | 2.01 | 3.23 | 5.46 | 160.2 → 144.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.64 | 3.13 | 12.42 | 100% | 0.53 | 2.73 | 4.37 | 104.1 → 101.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 2.79 | 12.72 | 99% | 0.51 | 2.45 | 3.97 | 81.1 → 73.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.57 | 3.04 | 17.71 | 99% | 0.56 | 2.62 | 4.33 | 106.7 → 98.4 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 5.04 | 18.60 | 99% | 1.40 | 3.08 | 5.33 | 106.7 → 98.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.65 | 3.15 | 12.60 | 100% | 0.59 | 2.71 | 4.29 | 105.9 → 97.6 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.61 | 3.01 | 12.06 | 100% | 0.47 | 2.68 | 4.32 | 83.9 → 78.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.71 | 3.27 | 18.58 | 99% | 0.59 | 2.81 | 4.70 | 109.3 → 101.2 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.81 | 4.75 | 16.18 | 99% | 1.49 | 3.04 | 4.51 | 109.4 → 101.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.74 | 10.63 | 100% | 0.52 | 2.38 | 3.15 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.186 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
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
| touch drag and flick (real touch input) | 46 | 0.50 | 2.85 | 3.14 | 100% | 0.34 | 2.49 | 4.12 | 50.2 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 253 frames at 120 Hz; the text stood still on 0 of them, 260 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.62 | 2.93 | 15.33 | 99% | 0.56 | 2.60 | 4.36 | 105.0 → 101.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.27 | 5.58 | 22.73 | 98% | 1.38 | 3.60 | 5.43 | 318.8 → 310.6 | 7 | 1.1 / 4 |

