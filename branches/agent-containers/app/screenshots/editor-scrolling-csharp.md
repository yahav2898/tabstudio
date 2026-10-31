# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 3.17 | 13.79 | 100% | 0.49 | 2.75 | 4.72 | 86.5 → 87.4 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.84 | 3.33 | 15.93 | 99% | 0.71 | 2.79 | 4.63 | 124.2 → 122.0 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.94 | 3.48 | 16.13 | 100% | 0.98 | 2.79 | 4.86 | 160.0 → 144.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.55 | 16.24 | 99% | 1.42 | 2.83 | 4.78 | 104.0 → 100.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.52 | 2.87 | 12.57 | 99% | 0.54 | 2.48 | 4.16 | 81.4 → 73.9 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.59 | 3.05 | 15.35 | 99% | 0.53 | 2.58 | 4.24 | 106.5 → 98.1 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.96 | 16.26 | 99% | 0.55 | 2.56 | 4.38 | 106.5 → 98.1 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.69 | 4.80 | 91.79 | 99% | 2.08 | 2.76 | 4.56 | 105.8 → 97.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 3.19 | 11.82 | 100% | 0.58 | 2.74 | 4.43 | 83.8 → 78.4 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.74 | 3.30 | 16.25 | 99% | 0.62 | 2.75 | 4.61 | 109.1 → 101.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.81 | 3.51 | 16.85 | 100% | 0.71 | 2.88 | 4.34 | 109.2 → 100.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 2.97 | 12.97 | 100% | 1.44 | 2.50 | 3.51 | 46.5 → 42.6 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| ArrangingLayoutable ScrollingTextLayer | 0.053 |
| ArrangingLayoutable TextView | 0.051 |
| AttachingStyle - | 0.014 |
| FindingResource - | 0.012 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.68 | 2.97 | 3.25 | 100% | 0.46 | 2.55 | 4.69 | 50.2 → 72.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 243 frames at 120 Hz; the text stood still on 0 of them, 265 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 3.15 | 16.15 | 99% | 0.55 | 2.67 | 4.58 | 104.8 → 101.5 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.39 | 5.52 | 19.80 | 98% | 2.44 | 5.02 | 4.82 | 318.5 → 310.4 | 8 | 1.1 / 4 |

