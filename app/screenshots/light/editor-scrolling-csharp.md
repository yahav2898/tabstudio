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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.45 | 2.47 | 12.96 | 99% | 0.36 | 1.97 | 2.73 | 86.1 → 86.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.63 | 3.54 | 15.02 | 99% | 1.01 | 2.52 | 4.62 | 123.4 → 121.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.56 | 2.38 | 14.06 | 99% | 0.57 | 1.94 | 2.77 | 158.6 → 142.9 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.47 | 2.24 | 12.59 | 100% | 0.41 | 1.93 | 2.76 | 103.2 → 100.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.37 | 2.08 | 13.11 | 99% | 0.41 | 1.80 | 2.55 | 80.6 → 73.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.39 | 2.09 | 13.59 | 99% | 0.36 | 1.83 | 2.59 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.47 | 2.31 | 14.05 | 99% | 0.42 | 1.95 | 2.78 | 105.7 → 97.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.62 | 3.50 | 16.19 | 99% | 2.34 | 2.34 | 3.17 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.43 | 2.25 | 10.57 | 100% | 0.37 | 1.94 | 2.65 | 83.4 → 78.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.47 | 2.33 | 13.66 | 99% | 0.42 | 1.98 | 2.72 | 108.3 → 100.4 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.43 | 2.20 | 10.67 | 99% | 0.40 | 1.92 | 2.33 | 108.5 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.27 | 2.12 | 8.12 | 100% | 0.54 | 1.84 | 2.15 | 46.0 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.135 |
| RaisingRoutedEvent ScrollBar | 0.040 |
| ArrangingLayoutable TextView | 0.008 |
| AttachingStyle - | 0.008 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable CachedLineNumberMargin | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| ArrangingLayoutable RepeatButton | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.35 | 1.98 | 2.10 | 100% | 0.28 | 1.80 | 2.56 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 4 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.56 | 2.96 | 15.80 | 99% | 0.74 | 2.25 | 3.30 | 104.2 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.59 | 2.32 | 15.11 | 98% | 0.38 | 2.02 | 2.56 | 316.6 → 308.5 | 7 | 1.1 / 4 |

