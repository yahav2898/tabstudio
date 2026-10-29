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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.91 | 3.70 | 11.78 | 99% | 0.50 | 3.21 | 4.91 | 124.7 → 126.3 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.13 | 4.13 | 14.72 | 99% | 0.80 | 3.43 | 5.29 | 162.1 → 160.3 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.38 | 4.61 | 18.64 | 99% | 1.22 | 3.52 | 5.32 | 197.8 → 181.8 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.10 | 4.05 | 13.81 | 99% | 0.68 | 3.44 | 5.15 | 142.5 → 139.7 | 3 | 1.1 / 4 |
| without line numbers | 368 | 1.00 | 3.98 | 12.99 | 99% | 0.74 | 3.31 | 5.35 | 119.3 → 112.5 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.21 | 4.39 | 18.97 | 99% | 0.86 | 3.49 | 5.59 | 144.4 → 137.1 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.10 | 3.99 | 14.09 | 99% | 0.68 | 3.42 | 5.28 | 144.7 → 136.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.05 | 3.97 | 16.80 | 99% | 0.69 | 3.31 | 5.05 | 143.9 → 135.8 | 3 | 1.2 / 30 |
| without coloring | 368 | 1.02 | 3.88 | 12.33 | 99% | 0.64 | 3.32 | 4.82 | 122.5 → 116.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.17 | 5.59 | 14.08 | 99% | 0.74 | 3.81 | 5.30 | 147.2 → 139.5 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.94 | 3.86 | 12.16 | 99% | 0.63 | 3.26 | 4.34 | 146.0 → 138.2 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.73 | 3.48 | 9.94 | 100% | 0.58 | 3.00 | 3.76 | 82.0 → 78.5 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.310 |
| ArrangingLayoutable Track | 0.050 |
| ArrangingLayoutable TextView | 0.021 |
| FindingResource - | 0.015 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.005 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.95 | 3.87 | 4.45 | 100% | 0.53 | 3.25 | 5.37 | 89.0 → 111.8 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2243 ms over 168 frames at 120 Hz; the text stood still on 0 of them, 203 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.01 | 3.83 | 13.09 | 99% | 0.67 | 3.26 | 5.06 | 143.0 → 139.5 | 4 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.36 | 4.16 | 17.44 | 98% | 0.70 | 3.51 | 4.85 | 354.0 → 347.2 | 8 | 1.1 / 4 |

