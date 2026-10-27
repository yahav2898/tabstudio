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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.34 | 1.30 | 6.59 | 100% | 0.22 | 1.12 | 2.26 | 117.8 → 119.1 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.43 | 1.44 | 8.64 | 100% | 0.36 | 1.16 | 2.39 | 156.1 → 153.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.47 | 1.51 | 9.25 | 99% | 0.42 | 1.16 | 2.42 | 191.6 → 175.9 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.40 | 1.73 | 9.85 | 100% | 0.35 | 1.29 | 2.47 | 135.5 → 133.3 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.44 | 2.83 | 70.65 | 99% | 0.46 | 2.28 | 2.65 | 112.5 → 106.0 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.36 | 1.33 | 8.40 | 100% | 0.32 | 1.10 | 2.38 | 137.8 → 129.8 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.36 | 1.30 | 10.28 | 100% | 0.32 | 1.11 | 2.31 | 137.9 → 129.9 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.35 | 1.33 | 8.64 | 99% | 0.31 | 1.09 | 2.29 | 137.1 → 129.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.36 | 1.29 | 7.38 | 100% | 0.28 | 1.11 | 2.24 | 115.0 → 109.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.38 | 1.33 | 7.69 | 100% | 0.31 | 1.11 | 2.32 | 140.3 → 132.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.35 | 1.33 | 7.94 | 100% | 0.31 | 1.11 | 1.98 | 140.0 → 132.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.28 | 2.24 | 9.85 | 100% | 0.34 | 1.05 | 1.72 | 76.1 → 73.0 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.221 |
| AttachingStyle - | 0.030 |
| ArrangingLayoutable CachedLineNumberMargin | 0.027 |
| ArrangingLayoutable Track | 0.016 |
| ArrangingLayoutable RepeatButton | 0.013 |
| ArrangingLayoutable TextView | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.31 | 1.23 | 8.37 | 98% | 0.25 | 1.05 | 2.48 | 82.7 → 104.5 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2236 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 0 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.38 | 1.34 | 8.54 | 100% | 0.30 | 1.12 | 2.34 | 136.2 → 133.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.59 | 1.59 | 10.73 | 98% | 0.36 | 1.29 | 2.43 | 348.4 → 341.9 | 7 | 1.1 / 4 |

