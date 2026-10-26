# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 6.17.0.1022, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 1.70 | 38.13 | 100% | 0.37 | 1.41 | 3.82 | 113.0 → 114.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 1.92 | 42.38 | 100% | 0.56 | 1.47 | 3.95 | 151.2 → 148.8 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.90 | 3.56 | 4.28 | 100% | 0.85 | 2.52 | 4.16 | 186.9 → 171.3 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.72 | 21.15 | 100% | 0.53 | 2.36 | 3.86 | 131.0 → 127.8 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 1.66 | 1.96 | 100% | 0.50 | 1.28 | 3.61 | 106.9 → 100.0 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.62 | 1.75 | 37.58 | 100% | 0.52 | 1.40 | 3.83 | 133.0 → 124.9 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.65 | 1.80 | 2.13 | 100% | 0.52 | 1.44 | 3.79 | 132.9 → 124.9 | 0 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.70 | 3.34 | 40.02 | 100% | 2.16 | 2.54 | 3.84 | 132.4 → 124.7 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 1.78 | 27.03 | 100% | 0.43 | 1.41 | 3.66 | 109.8 → 104.9 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.66 | 1.80 | 2.19 | 100% | 0.51 | 1.41 | 3.80 | 135.5 → 127.8 | 0 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 1.75 | 38.13 | 100% | 0.52 | 1.40 | 3.43 | 134.9 → 127.4 | 1 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.41 | 1.44 | 1.86 | 100% | 0.44 | 1.14 | 2.80 | 71.1 → 67.6 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.233 |
| ArrangingLayoutable ScrollingTextLayer | 0.172 |
| AttachingStyle - | 0.047 |
| ArrangingLayoutable TextView | 0.022 |
| ArrangingLayoutable LineDrawing | 0.018 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.59 | 2.76 | 2.82 | 100% | 0.48 | 2.29 | 4.00 | 77.3 → 100.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 115 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 1.86 | 34.89 | 100% | 0.50 | 1.45 | 3.85 | 131.3 → 128.5 | 1 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.01 | 2.18 | 46.51 | 100% | 0.57 | 1.71 | 3.93 | 343.5 → 337.0 | 1 | 1.1 / 4 |

