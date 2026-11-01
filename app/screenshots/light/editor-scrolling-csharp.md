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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.66 | 3.17 | 19.16 | 99% | 0.46 | 2.74 | 4.63 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 3.41 | 21.03 | 99% | 0.70 | 2.83 | 4.69 | 121.3 → 121.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.11 | 5.38 | 32.44 | 99% | 2.54 | 3.06 | 5.34 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.68 | 3.29 | 19.39 | 99% | 0.60 | 2.76 | 4.60 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.59 | 3.08 | 13.94 | 99% | 0.65 | 2.56 | 4.48 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.67 | 3.18 | 18.16 | 99% | 0.63 | 2.64 | 4.67 | 104.3 → 96.0 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.77 | 3.40 | 14.41 | 100% | 0.64 | 2.76 | 4.81 | 104.1 → 96.0 | 3 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.81 | 3.63 | 16.12 | 99% | 0.92 | 2.78 | 5.30 | 103.6 → 95.3 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.61 | 2.93 | 15.28 | 100% | 0.49 | 2.54 | 4.26 | 82.2 → 76.9 | 1 | 1.2 / 29 |
| without squiggles | 368 | 0.72 | 3.19 | 19.40 | 99% | 0.64 | 2.69 | 4.66 | 106.7 → 98.8 | 3 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.70 | 3.15 | 17.90 | 99% | 0.58 | 2.70 | 4.01 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.54 | 2.94 | 13.95 | 100% | 1.31 | 2.45 | 3.62 | 46.1 → 42.4 | 2 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.382 |
| AttachingStyle - | 0.170 |
| ArrangingLayoutable TextView | 0.031 |
| ArrangingLayoutable ScrollingTextLayer | 0.018 |
| FindingResource - | 0.014 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.67 | 3.21 | 3.52 | 100% | 0.48 | 2.71 | 4.63 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 230 frames at 120 Hz; the text stood still on 0 of them, 262 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.74 | 3.39 | 18.18 | 99% | 0.61 | 2.85 | 4.70 | 102.6 → 99.3 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.53 | 5.78 | 23.72 | 98% | 2.37 | 5.22 | 5.19 | 307.6 → 299.9 | 8 | 1.1 / 4 |

