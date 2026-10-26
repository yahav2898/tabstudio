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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.80 | 2.77 | 12.00 | 99% | 0.48 | 2.39 | 4.85 | 117.6 → 119.0 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.83 | 3.12 | 14.12 | 99% | 0.67 | 2.43 | 4.17 | 155.9 → 152.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.83 | 2.34 | 13.48 | 99% | 0.65 | 1.75 | 3.89 | 190.9 → 175.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.73 | 2.11 | 11.64 | 99% | 0.53 | 1.70 | 3.84 | 135.2 → 132.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.71 | 2.20 | 11.43 | 99% | 0.56 | 1.72 | 3.84 | 111.8 → 105.6 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.84 | 2.30 | 14.38 | 99% | 0.56 | 1.83 | 4.06 | 137.7 → 129.5 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.81 | 2.98 | 14.68 | 99% | 0.62 | 2.31 | 4.14 | 137.6 → 129.7 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.82 | 3.06 | 12.11 | 99% | 0.60 | 2.51 | 4.03 | 137.4 → 129.0 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.81 | 2.37 | 11.60 | 99% | 0.52 | 1.91 | 3.86 | 115.0 → 110.1 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.95 | 2.86 | 13.83 | 99% | 0.62 | 2.43 | 4.23 | 140.4 → 132.7 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.90 | 2.96 | 17.80 | 99% | 0.63 | 2.44 | 3.90 | 140.5 → 132.6 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.71 | 2.53 | 9.39 | 100% | 0.56 | 2.13 | 3.33 | 77.1 → 73.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.350 |
| AttachingStyle - | 0.053 |
| RaisingRoutedEvent ScrollViewer | 0.042 |
| ArrangingLayoutable TextView | 0.031 |
| ArrangingLayoutable LineDrawing | 0.021 |
| ArrangingLayoutable CachedLineNumberMargin | 0.020 |
| ArrangingLayoutable Track | 0.018 |
| ArrangingLayoutable NumberDrawing | 0.015 |
| FindingResource - | 0.014 |
| ArrangingLayoutable RepeatButton | 0.014 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.80 | 3.10 | 4.84 | 100% | 0.64 | 2.46 | 4.65 | 82.8 → 104.5 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 258 frames at 120 Hz; the text stood still on 0 of them, 266 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.93 | 3.15 | 12.75 | 99% | 0.62 | 2.60 | 4.25 | 136.1 → 133.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.29 | 3.50 | 18.60 | 98% | 0.68 | 2.89 | 4.39 | 348.3 → 342.3 | 8 | 1.1 / 4 |

