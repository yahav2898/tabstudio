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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.76 | 3.23 | 14.35 | 99% | 0.52 | 2.71 | 4.69 | 117.7 → 119.6 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 2.73 | 11.62 | 99% | 0.60 | 2.03 | 4.24 | 155.1 → 152.6 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 2.58 | 14.02 | 99% | 0.66 | 2.15 | 4.30 | 189.9 → 174.3 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 2.84 | 11.31 | 99% | 0.50 | 2.47 | 4.21 | 134.7 → 132.0 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.62 | 2.94 | 11.85 | 99% | 0.57 | 2.47 | 4.31 | 111.8 → 105.3 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.63 | 2.98 | 11.96 | 99% | 0.53 | 2.53 | 4.24 | 137.1 → 129.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.67 | 3.03 | 12.36 | 99% | 0.51 | 2.60 | 4.33 | 137.0 → 129.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.64 | 2.95 | 12.17 | 99% | 0.50 | 2.55 | 4.05 | 136.2 → 128.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.70 | 3.35 | 13.87 | 99% | 0.50 | 2.80 | 4.32 | 114.9 → 110.3 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 2.93 | 12.43 | 99% | 0.49 | 2.52 | 4.05 | 139.5 → 132.1 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 2.96 | 11.73 | 99% | 0.50 | 2.54 | 3.60 | 139.6 → 131.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.40 | 2.59 | 10.36 | 100% | 0.43 | 2.29 | 3.04 | 76.7 → 73.3 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.260 |
| ArrangingLayoutable NumberDrawing | 0.026 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable CachedLineNumberMargin | 0.019 |
| ArrangingLayoutable TextView | 0.014 |
| ArrangingLayoutable Thumb | 0.012 |
| FindingResource - | 0.010 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.71 | 4.92 | 5.48 | 100% | 0.45 | 4.63 | 4.70 | 82.7 → 104.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 230 frames at 120 Hz; the text stood still on 0 of them, 264 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.68 | 2.92 | 12.06 | 99% | 0.52 | 2.54 | 4.25 | 135.3 → 132.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.10 | 3.56 | 16.11 | 98% | 0.64 | 3.00 | 4.36 | 347.2 → 340.6 | 8 | 1.1 / 4 |

