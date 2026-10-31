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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.41 | 1.64 | 44.35 | 100% | 0.27 | 1.38 | 3.26 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.57 | 2.78 | 119.16 | 100% | 1.29 | 2.32 | 3.66 | 121.3 → 121.7 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.63 | 3.06 | 25.69 | 99% | 2.46 | 2.34 | 3.52 | 156.7 → 141.7 | 1 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.46 | 1.73 | 2.32 | 100% | 0.41 | 1.43 | 3.33 | 101.6 → 98.8 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.38 | 1.60 | 46.37 | 100% | 0.40 | 1.30 | 3.14 | 79.8 → 73.0 | 1 | 1.2 / 29 |
| without the folding margin | 368 | 0.44 | 1.68 | 2.53 | 100% | 0.42 | 1.37 | 3.27 | 104.3 → 96.0 | 0 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.47 | 1.77 | 49.56 | 100% | 0.42 | 1.48 | 3.49 | 104.1 → 96.0 | 1 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.44 | 1.72 | 2.68 | 100% | 0.42 | 1.41 | 3.26 | 103.5 → 95.3 | 0 | 1.2 / 29 |
| without coloring | 368 | 0.43 | 1.61 | 1.85 | 100% | 0.35 | 1.37 | 3.17 | 82.2 → 76.9 | 0 | 1.2 / 29 |
| without squiggles | 368 | 0.51 | 2.71 | 98.25 | 99% | 0.59 | 2.29 | 3.55 | 106.7 → 98.8 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.50 | 2.59 | 5.94 | 100% | 0.93 | 2.23 | 2.93 | 106.8 → 98.6 | 0 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.29 | 1.47 | 31.76 | 100% | 0.47 | 1.22 | 2.31 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.144 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable ScrollingTextLayer | 0.004 |
| ArrangingLayoutable NumberDrawing | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.000 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.38 | 1.61 | 1.83 | 100% | 0.28 | 1.34 | 3.40 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 0 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.47 | 1.69 | 67.51 | 100% | 0.40 | 1.39 | 3.33 | 102.6 → 99.2 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.82 | 4.00 | 81.47 | 100% | 2.30 | 3.67 | 3.46 | 307.5 → 299.7 | 2 | 1.1 / 4 |

