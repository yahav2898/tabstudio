# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.53 | 1.81 | 47.41 | 100% | 0.32 | 1.55 | 3.75 | 118.7 → 120.5 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 1.96 | 51.99 | 100% | 0.51 | 1.59 | 3.92 | 156.6 → 154.5 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.72 | 1.96 | 2.35 | 100% | 0.60 | 1.51 | 3.84 | 191.8 → 175.8 | 0 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 3.88 | 47.84 | 100% | 0.59 | 3.47 | 4.15 | 137.1 → 134.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.49 | 1.81 | 2.71 | 100% | 0.47 | 1.51 | 3.81 | 112.9 → 106.3 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 1.97 | 37.33 | 100% | 0.50 | 1.63 | 4.45 | 139.2 → 130.6 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.58 | 1.86 | 2.86 | 100% | 0.48 | 1.55 | 4.02 | 138.5 → 130.9 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.57 | 1.94 | 2.34 | 100% | 0.46 | 1.60 | 3.94 | 137.5 → 130.2 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.55 | 2.37 | 48.57 | 100% | 0.42 | 1.68 | 3.67 | 115.9 → 111.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.61 | 3.11 | 4.21 | 100% | 0.68 | 2.32 | 4.28 | 141.7 → 133.1 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.56 | 1.54 | 34.67 | 100% | 0.42 | 1.23 | 3.21 | 140.6 → 132.5 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.38 | 1.40 | 1.74 | 100% | 0.37 | 1.08 | 2.70 | 76.8 → 74.0 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.161 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.56 | 1.74 | 1.77 | 100% | 0.38 | 1.36 | 4.80 | 83.7 → 105.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 1 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 2.86 | 46.82 | 100% | 0.55 | 2.47 | 4.89 | 137.3 → 135.1 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.87 | 2.14 | 55.84 | 99% | 0.51 | 1.67 | 4.02 | 348.5 → 341.8 | 2 | 1.1 / 4 |

