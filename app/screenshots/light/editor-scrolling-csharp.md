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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.47 | 2.53 | 11.20 | 99% | 0.29 | 2.30 | 3.32 | 122.4 → 123.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.63 | 2.72 | 11.67 | 99% | 0.45 | 2.36 | 3.42 | 160.0 → 157.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.68 | 2.77 | 14.99 | 99% | 0.56 | 2.33 | 3.42 | 195.2 → 179.1 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 3.10 | 17.35 | 99% | 0.49 | 2.60 | 3.73 | 140.3 → 137.1 | 5 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.74 | 11.92 | 99% | 0.46 | 2.41 | 3.54 | 117.0 → 109.6 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.54 | 2.61 | 17.23 | 99% | 0.44 | 2.34 | 3.39 | 142.1 → 134.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.55 | 2.70 | 11.71 | 99% | 0.44 | 2.36 | 3.38 | 142.0 → 134.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.53 | 2.65 | 11.42 | 99% | 0.43 | 2.32 | 3.33 | 141.6 → 133.5 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.51 | 2.60 | 10.19 | 99% | 0.36 | 2.32 | 3.20 | 120.0 → 114.5 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.66 | 3.12 | 16.77 | 99% | 0.53 | 2.58 | 3.74 | 144.7 → 137.5 | 5 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 2.91 | 12.29 | 99% | 0.46 | 2.57 | 3.30 | 144.8 → 136.5 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.38 | 7.43 | 100% | 0.36 | 2.11 | 2.47 | 80.4 → 77.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.189 |
| ArrangingLayoutable TextView | 0.011 |
| AttachingStyle - | 0.010 |
| FindingResource - | 0.007 |
| ArrangingLayoutable CachedLineNumberMargin | 0.007 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.49 | 2.45 | 2.55 | 100% | 0.35 | 2.25 | 3.51 | 87.7 → 107.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 23 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.56 | 2.62 | 11.48 | 99% | 0.42 | 2.34 | 3.35 | 140.4 → 137.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.03 | 5.40 | 23.88 | 98% | 2.36 | 4.90 | 3.85 | 352.2 → 347.0 | 9 | 1.1 / 4 |

