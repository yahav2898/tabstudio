# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.75 | 3.77 | 18.92 | 99% | 0.50 | 3.18 | 4.67 | 86.7 → 87.7 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.80 | 3.67 | 14.07 | 99% | 0.81 | 3.04 | 4.98 | 124.5 → 122.3 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.87 | 3.57 | 18.33 | 99% | 0.83 | 2.93 | 4.48 | 160.2 → 144.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 3.26 | 16.50 | 99% | 0.56 | 2.84 | 4.30 | 104.1 → 101.2 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.54 | 3.11 | 14.30 | 99% | 0.58 | 2.69 | 4.23 | 81.1 → 73.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.92 | 5.19 | 93.27 | 99% | 0.92 | 3.23 | 4.96 | 106.8 → 98.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.93 | 3.76 | 18.44 | 99% | 0.67 | 3.15 | 4.79 | 106.7 → 98.4 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.90 | 3.78 | 18.59 | 99% | 0.73 | 3.16 | 4.72 | 106.0 → 97.7 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.79 | 3.61 | 13.03 | 99% | 0.58 | 3.03 | 4.48 | 83.9 → 78.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.98 | 3.87 | 17.82 | 99% | 1.80 | 3.21 | 5.06 | 109.3 → 101.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 3.59 | 16.43 | 99% | 0.60 | 3.02 | 4.09 | 109.4 → 101.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.56 | 3.22 | 12.71 | 100% | 1.46 | 2.77 | 3.47 | 46.1 → 42.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.281 |
| ArrangingLayoutable NumberDrawing | 0.063 |
| ArrangingLayoutable TextView | 0.016 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.78 | 3.47 | 3.60 | 100% | 0.48 | 2.95 | 4.62 | 50.3 → 72.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 209 frames at 120 Hz; the text stood still on 0 of them, 242 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 5.27 | 16.49 | 99% | 1.89 | 3.28 | 5.12 | 105.1 → 101.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.30 | 4.12 | 20.47 | 98% | 0.77 | 3.40 | 4.80 | 318.7 → 310.7 | 7 | 1.1 / 4 |

