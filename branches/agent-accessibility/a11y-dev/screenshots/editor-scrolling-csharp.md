# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1014, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.44 | 1.67 | 2.37 | 100% | 0.29 | 1.44 | 3.34 | 84.4 → 85.2 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.55 | 1.86 | 41.22 | 100% | 0.45 | 1.49 | 3.55 | 121.3 → 121.7 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.68 | 2.85 | 45.83 | 100% | 1.33 | 2.19 | 3.70 | 156.7 → 141.8 | 2 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.50 | 1.84 | 3.78 | 100% | 0.42 | 1.48 | 3.51 | 101.6 → 98.8 | 0 | 1.1 / 4 |
| without line numbers | 368 | 0.40 | 1.65 | 2.05 | 100% | 0.41 | 1.37 | 3.30 | 79.7 → 73.2 | 0 | 1.2 / 29 |
| without the folding margin | 368 | 0.47 | 1.75 | 39.71 | 100% | 0.43 | 1.43 | 3.49 | 104.3 → 96.0 | 1 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.47 | 1.78 | 2.45 | 100% | 0.41 | 1.44 | 3.45 | 104.1 → 96.0 | 0 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.50 | 2.83 | 45.56 | 100% | 2.34 | 2.31 | 3.49 | 103.5 → 95.4 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.43 | 1.73 | 2.38 | 100% | 0.36 | 1.45 | 3.38 | 81.9 → 76.7 | 0 | 1.2 / 29 |
| without squiggles | 368 | 0.48 | 1.78 | 38.02 | 100% | 0.42 | 1.48 | 3.50 | 106.7 → 98.8 | 1 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.47 | 1.80 | 2.44 | 100% | 0.42 | 1.50 | 3.05 | 106.8 → 98.6 | 0 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.31 | 1.55 | 2.15 | 100% | 0.41 | 1.27 | 2.44 | 45.6 → 42.0 | 0 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 61.3

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.219 |
| AttachingStyle - | 0.045 |
| ArrangingLayoutable NumberDrawing | 0.014 |
| MeasuringLayoutable CachedLineNumberMargin | 0.012 |
| ArrangingLayoutable TextView | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable LineDrawing | 0.006 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.39 | 1.73 | 1.88 | 100% | 0.29 | 1.38 | 3.58 | 49.2 → 73.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 25 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.48 | 1.79 | 2.29 | 100% | 0.42 | 1.48 | 3.51 | 102.6 → 99.2 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.79 | 2.97 | 47.06 | 100% | 0.55 | 2.52 | 3.58 | 306.2 → 298.4 | 2 | 1.1 / 4 |

