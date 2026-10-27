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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.65 | 2.12 | 11.88 | 99% | 0.41 | 1.72 | 3.83 | 119.6 → 120.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 2.34 | 12.65 | 99% | 0.56 | 1.85 | 3.97 | 157.8 → 155.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.89 | 3.45 | 15.90 | 99% | 0.83 | 2.49 | 4.12 | 193.4 → 178.4 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.87 | 2.79 | 12.21 | 99% | 0.56 | 2.14 | 4.23 | 137.9 → 134.8 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.65 | 2.10 | 14.52 | 99% | 0.52 | 1.68 | 3.82 | 114.3 → 107.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.70 | 2.13 | 15.21 | 99% | 0.51 | 1.76 | 3.80 | 139.9 → 131.5 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.14 | 10.24 | 99% | 0.49 | 1.71 | 3.76 | 139.5 → 131.0 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 2.48 | 8.48 | 100% | 0.47 | 2.17 | 3.62 | 138.4 → 131.3 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 2.91 | 10.45 | 99% | 0.51 | 2.46 | 3.76 | 117.2 → 112.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 2.29 | 11.33 | 99% | 0.50 | 1.93 | 3.84 | 142.3 → 134.8 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.57 | 2.38 | 11.66 | 99% | 0.46 | 2.08 | 3.18 | 141.9 → 133.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 2.18 | 7.43 | 100% | 0.41 | 1.96 | 2.82 | 78.2 → 74.8 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.203 |
| ArrangingLayoutable TextView | 0.017 |
| FindingResource - | 0.012 |
| AttachingStyle - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.58 | 2.54 | 2.91 | 100% | 0.37 | 2.28 | 3.89 | 85.1 → 105.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 64 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.86 | 3.08 | 14.39 | 99% | 0.54 | 2.56 | 4.18 | 138.7 → 135.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.07 | 3.12 | 17.78 | 98% | 0.58 | 2.64 | 3.90 | 350.4 → 343.9 | 8 | 1.1 / 4 |

