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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.87 | 3.28 | 13.19 | 100% | 0.53 | 2.84 | 4.73 | 86.5 → 87.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.15 | 3.78 | 18.56 | 99% | 0.86 | 3.02 | 5.09 | 124.3 → 122.0 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.34 | 5.37 | 19.43 | 98% | 2.77 | 3.14 | 5.46 | 160.0 → 144.3 | 2 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.02 | 3.57 | 16.98 | 99% | 0.74 | 2.95 | 5.05 | 104.0 → 101.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.81 | 3.40 | 14.11 | 99% | 0.75 | 2.75 | 4.89 | 81.4 → 74.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.02 | 3.66 | 14.71 | 99% | 0.83 | 2.95 | 5.14 | 106.6 → 98.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.08 | 3.62 | 18.08 | 100% | 0.83 | 2.97 | 5.26 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.96 | 3.55 | 16.12 | 99% | 0.72 | 2.90 | 4.94 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.89 | 3.47 | 13.06 | 100% | 0.65 | 2.89 | 4.78 | 83.9 → 78.4 | 1 | 1.2 / 30 |
| without squiggles | 368 | 1.10 | 5.04 | 15.99 | 99% | 1.62 | 3.15 | 5.21 | 109.2 → 101.0 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.95 | 3.61 | 15.27 | 99% | 0.69 | 3.00 | 4.48 | 109.2 → 100.8 | 2 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.62 | 3.10 | 13.97 | 100% | 0.69 | 2.59 | 3.74 | 46.5 → 42.5 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.278 |
| FindingResource - | 0.053 |
| ArrangingLayoutable TextView | 0.022 |
| AttachingStyle - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.013 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.80 | 3.36 | 13.31 | 98% | 0.51 | 2.75 | 5.06 | 50.2 → 72.6 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2249 ms over 182 frames at 120 Hz; the text stood still on 0 of them, 215 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.03 | 3.65 | 11.44 | 100% | 0.77 | 3.00 | 5.09 | 105.0 → 101.5 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.49 | 4.19 | 19.61 | 98% | 0.90 | 3.36 | 5.08 | 318.4 → 310.3 | 7 | 1.1 / 4 |

