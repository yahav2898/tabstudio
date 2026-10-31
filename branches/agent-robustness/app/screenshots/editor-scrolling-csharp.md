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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 2.23 | 11.21 | 100% | 0.37 | 1.96 | 2.73 | 84.8 → 85.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 3.78 | 15.66 | 99% | 2.34 | 2.40 | 3.48 | 122.1 → 122.6 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 3.23 | 15.85 | 99% | 1.40 | 2.35 | 3.64 | 158.1 → 143.0 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.78 | 15.77 | 99% | 0.62 | 2.26 | 3.50 | 102.2 → 99.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.60 | 11.90 | 99% | 0.56 | 2.09 | 3.06 | 80.5 → 73.6 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.73 | 2.74 | 13.78 | 100% | 0.56 | 2.17 | 3.22 | 105.0 → 96.6 | 1 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.55 | 2.50 | 14.78 | 99% | 0.52 | 2.07 | 2.97 | 104.9 → 96.6 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.63 | 2.56 | 15.36 | 99% | 0.54 | 2.13 | 3.02 | 104.2 → 96.0 | 4 | 1.2 / 29 |
| without coloring | 368 | 0.66 | 2.99 | 14.89 | 100% | 0.53 | 2.35 | 3.16 | 82.6 → 77.3 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.51 | 2.53 | 13.17 | 99% | 0.49 | 2.10 | 3.03 | 107.5 → 99.4 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.46 | 2.31 | 16.27 | 99% | 0.46 | 1.98 | 2.39 | 107.5 → 99.2 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.32 | 2.13 | 10.61 | 100% | 0.45 | 1.83 | 1.94 | 46.5 → 42.7 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.160 |
| AttachingStyle - | 0.053 |
| FindingResource - | 0.009 |
| ArrangingLayoutable TextView | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.36 | 2.17 | 2.79 | 100% | 0.41 | 1.81 | 3.16 | 49.3 → 73.3 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 16 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 2.99 | 22.47 | 99% | 0.97 | 2.43 | 3.31 | 103.3 → 99.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.77 | 2.89 | 22.05 | 98% | 0.50 | 2.28 | 2.98 | 308.8 → 301.0 | 7 | 1.1 / 4 |

