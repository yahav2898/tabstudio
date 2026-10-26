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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 2.63 | 15.35 | 99% | 0.41 | 2.26 | 4.40 | 113.5 → 114.5 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.71 | 2.10 | 15.09 | 99% | 0.53 | 1.70 | 4.13 | 151.1 → 148.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.80 | 2.24 | 14.12 | 99% | 0.69 | 1.71 | 4.17 | 186.8 → 171.1 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.61 | 2.00 | 10.37 | 99% | 0.48 | 1.65 | 4.01 | 130.8 → 128.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.82 | 69.68 | 99% | 0.65 | 1.77 | 4.24 | 107.8 → 101.1 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 1.97 | 13.46 | 99% | 0.49 | 1.62 | 3.98 | 133.2 → 125.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.59 | 2.08 | 10.97 | 99% | 0.61 | 1.71 | 4.61 | 133.0 → 125.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.58 | 1.94 | 10.48 | 99% | 0.47 | 1.62 | 3.92 | 132.4 → 124.3 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.64 | 2.74 | 10.50 | 100% | 0.50 | 1.98 | 4.22 | 110.4 → 105.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.62 | 2.11 | 10.64 | 99% | 0.49 | 1.68 | 4.06 | 135.7 → 128.0 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.58 | 1.90 | 12.98 | 99% | 0.45 | 1.57 | 3.46 | 135.1 → 127.3 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.35 | 1.61 | 10.09 | 100% | 0.39 | 1.34 | 2.93 | 71.7 → 68.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.157 |
| AttachingStyle - | 0.045 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| FindingResource - | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.53 | 1.98 | 13.01 | 98% | 0.40 | 1.74 | 4.78 | 78.2 → 100.1 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 258 frames at 120 Hz; the text stood still on 0 of them, 49 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.60 | 2.02 | 9.22 | 99% | 0.48 | 1.64 | 3.98 | 131.5 → 128.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.02 | 2.46 | 14.36 | 98% | 0.58 | 1.94 | 4.11 | 343.5 → 336.9 | 8 | 1.1 / 4 |

