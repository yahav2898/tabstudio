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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.67 | 2.13 | 45.85 | 100% | 0.42 | 1.75 | 4.62 | 120.0 → 121.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.91 | 4.03 | 4.88 | 100% | 0.74 | 3.53 | 4.91 | 158.6 → 156.3 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.91 | 2.28 | 2.77 | 100% | 0.74 | 1.71 | 4.84 | 193.5 → 177.8 | 0 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.75 | 2.06 | 44.25 | 100% | 0.56 | 1.64 | 4.71 | 137.6 → 134.8 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 1.89 | 2.71 | 100% | 0.56 | 1.50 | 4.48 | 114.6 → 107.8 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.73 | 2.12 | 42.11 | 100% | 0.58 | 1.64 | 4.68 | 140.2 → 132.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.77 | 3.81 | 92.51 | 99% | 0.67 | 2.52 | 4.83 | 140.4 → 132.2 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 1.94 | 2.31 | 100% | 0.56 | 1.55 | 4.45 | 139.1 → 131.2 | 0 | 1.2 / 30 |
| without coloring | 368 | 0.67 | 1.88 | 42.56 | 100% | 0.46 | 1.53 | 4.37 | 117.2 → 112.2 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.73 | 2.01 | 43.02 | 100% | 0.54 | 1.60 | 4.62 | 142.4 → 135.0 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.72 | 2.01 | 2.37 | 100% | 0.55 | 1.61 | 4.04 | 142.5 → 134.5 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 2.53 | 3.80 | 100% | 0.51 | 1.70 | 3.36 | 78.3 → 75.3 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.219 |
| AttachingStyle - | 0.035 |
| ArrangingLayoutable TextView | 0.021 |
| ArrangingLayoutable Track | 0.017 |
| ArrangingLayoutable ScrollingTextLayer | 0.017 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| ArrangingLayoutable Thumb | 0.012 |
| ArrangingLayoutable LineDrawing | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.66 | 2.01 | 2.12 | 100% | 0.48 | 1.62 | 4.91 | 85.1 → 106.7 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 265 frames at 120 Hz; the text stood still on 0 of them, 56 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 2.07 | 2.39 | 100% | 0.55 | 1.65 | 4.61 | 138.0 → 135.3 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.08 | 2.41 | 52.10 | 99% | 0.61 | 1.89 | 4.73 | 349.8 → 343.3 | 2 | 1.1 / 4 |

