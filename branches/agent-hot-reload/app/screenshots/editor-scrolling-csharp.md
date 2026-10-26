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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.59 | 1.87 | 8.98 | 99% | 0.41 | 1.54 | 3.77 | 113.0 → 114.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.82 | 2.20 | 11.50 | 99% | 0.63 | 1.66 | 4.08 | 151.9 → 149.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.08 | 2.61 | 14.27 | 99% | 0.82 | 1.89 | 4.27 | 188.5 → 172.5 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.95 | 3.29 | 12.55 | 99% | 0.62 | 2.52 | 4.38 | 131.9 → 128.9 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.78 | 2.72 | 10.29 | 99% | 0.62 | 1.70 | 3.98 | 108.0 → 101.4 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.89 | 2.30 | 12.97 | 99% | 0.61 | 1.76 | 4.42 | 134.1 → 125.8 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.84 | 2.19 | 11.35 | 99% | 0.60 | 1.68 | 3.98 | 134.0 → 125.8 | 2 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.72 | 2.19 | 10.89 | 99% | 0.59 | 1.67 | 3.89 | 132.8 → 125.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.89 | 3.06 | 9.66 | 99% | 0.63 | 2.49 | 4.09 | 111.0 → 106.1 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.83 | 2.12 | 11.66 | 99% | 0.58 | 1.66 | 4.00 | 136.5 → 128.6 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.10 | 10.43 | 99% | 0.57 | 1.60 | 3.57 | 136.3 → 128.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.46 | 1.71 | 8.43 | 100% | 0.45 | 1.33 | 2.93 | 71.4 → 68.0 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.234 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.011 |
| AttachingStyle - | 0.011 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable Track | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.85 | 2.06 | 2.12 | 100% | 0.48 | 1.57 | 4.07 | 78.4 → 99.4 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2237 ms over 267 frames at 120 Hz; the text stood still on 0 of them, 36 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.75 | 2.05 | 10.48 | 99% | 0.51 | 1.58 | 3.90 | 131.9 → 129.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.18 | 2.53 | 14.57 | 98% | 0.63 | 1.91 | 4.12 | 345.5 → 338.9 | 8 | 1.1 / 4 |

