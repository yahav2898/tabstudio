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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.78 | 3.50 | 13.74 | 100% | 0.56 | 2.93 | 5.22 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.94 | 3.66 | 16.52 | 99% | 0.74 | 2.97 | 5.06 | 121.3 → 121.8 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.44 | 5.75 | 36.36 | 99% | 2.14 | 4.85 | 5.73 | 156.8 → 141.8 | 4 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 3.42 | 27.13 | 99% | 0.68 | 2.86 | 5.34 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.73 | 3.26 | 16.27 | 99% | 0.68 | 2.70 | 4.99 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.84 | 3.56 | 16.87 | 99% | 0.74 | 2.82 | 5.07 | 104.3 → 96.1 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.90 | 3.47 | 18.06 | 99% | 0.73 | 2.82 | 5.70 | 104.2 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 1.08 | 3.91 | 17.24 | 99% | 1.44 | 2.98 | 5.43 | 103.5 → 95.4 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.95 | 3.55 | 14.32 | 99% | 0.69 | 2.89 | 5.15 | 82.3 → 77.0 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.89 | 3.45 | 15.17 | 99% | 0.70 | 2.80 | 5.08 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.80 | 3.43 | 18.23 | 99% | 0.65 | 2.79 | 4.58 | 106.8 → 98.6 | 3 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.56 | 3.10 | 9.84 | 100% | 1.19 | 2.51 | 3.90 | 46.1 → 42.4 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.454 |
| ArrangingLayoutable TextView | 0.052 |
| ArrangingLayoutable CachedLineNumberMargin | 0.032 |
| AttachingStyle - | 0.031 |
| FindingResource - | 0.015 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.82 | 3.11 | 3.51 | 100% | 0.45 | 2.73 | 5.09 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2245 ms over 214 frames at 120 Hz; the text stood still on 0 of them, 246 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.87 | 3.54 | 23.71 | 99% | 0.72 | 2.91 | 5.22 | 102.7 → 99.2 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.52 | 5.35 | 22.58 | 98% | 0.99 | 3.39 | 5.46 | 307.7 → 299.8 | 8 | 1.1 / 4 |

