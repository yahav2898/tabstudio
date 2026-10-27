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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.73 | 2.32 | 10.23 | 99% | 0.46 | 1.89 | 4.43 | 119.6 → 120.7 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.90 | 2.61 | 12.58 | 99% | 0.65 | 1.99 | 4.57 | 157.1 → 154.9 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.00 | 2.71 | 14.30 | 99% | 0.85 | 2.05 | 4.68 | 192.4 → 176.8 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.00 | 3.49 | 15.64 | 99% | 0.72 | 2.52 | 5.18 | 137.8 → 135.5 | 4 | 1.1 / 4 |
| without line numbers | 368 | 0.75 | 2.40 | 11.81 | 99% | 0.58 | 1.90 | 4.55 | 114.0 → 107.2 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.80 | 2.42 | 14.79 | 99% | 0.61 | 1.91 | 4.57 | 139.2 → 131.4 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 2.45 | 11.94 | 99% | 0.58 | 2.01 | 4.55 | 139.3 → 131.3 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.98 | 3.34 | 12.34 | 99% | 0.74 | 2.46 | 4.97 | 138.7 → 131.7 | 4 | 1.2 / 30 |
| without coloring | 368 | 0.84 | 2.60 | 10.35 | 99% | 0.56 | 2.11 | 4.71 | 117.5 → 111.9 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.84 | 2.39 | 11.71 | 99% | 0.60 | 1.94 | 4.57 | 141.9 → 134.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.78 | 2.45 | 11.94 | 99% | 0.58 | 1.95 | 4.03 | 141.8 → 134.1 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 2.17 | 8.14 | 100% | 0.48 | 1.76 | 3.52 | 77.9 → 74.8 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.318 |
| AttachingStyle - | 0.103 |
| ArrangingLayoutable CachedLineNumberMargin | 0.032 |
| ArrangingLayoutable TextView | 0.032 |
| ArrangingLayoutable LineDrawing | 0.031 |
| ArrangingLayoutable NumberDrawing | 0.027 |
| ArrangingLayoutable ScrollingTextLayer | 0.019 |
| FindingResource - | 0.014 |
| ArrangingLayoutable Border | 0.012 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable RepeatButton | 0.004 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.70 | 2.16 | 8.50 | 98% | 0.47 | 1.78 | 4.67 | 85.1 → 106.3 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 150 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.80 | 2.42 | 12.72 | 99% | 0.59 | 1.98 | 4.54 | 137.6 → 134.8 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.34 | 3.75 | 20.59 | 98% | 0.78 | 2.97 | 4.96 | 348.8 → 343.2 | 9 | 1.1 / 4 |

