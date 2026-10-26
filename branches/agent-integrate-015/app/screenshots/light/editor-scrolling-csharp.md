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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 1.82 | 9.15 | 100% | 0.38 | 1.51 | 3.48 | 116.1 → 117.7 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.68 | 2.05 | 10.48 | 99% | 0.53 | 1.64 | 3.55 | 154.4 → 151.5 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.73 | 2.84 | 23.41 | 99% | 0.63 | 1.83 | 3.93 | 189.1 → 174.1 | 5 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.74 | 3.29 | 69.26 | 99% | 0.58 | 2.58 | 3.85 | 134.7 → 131.5 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.48 | 1.72 | 8.58 | 100% | 0.43 | 1.40 | 3.27 | 110.3 → 103.7 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.55 | 1.75 | 9.58 | 99% | 0.44 | 1.47 | 3.41 | 135.7 → 128.0 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 2.01 | 10.66 | 99% | 0.48 | 1.60 | 3.65 | 136.3 → 128.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.61 | 1.96 | 9.87 | 99% | 0.48 | 1.56 | 3.47 | 135.5 → 127.4 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.75 | 3.09 | 10.70 | 99% | 0.55 | 2.47 | 3.76 | 114.1 → 109.5 | 4 | 1.2 / 30 |
| without squiggles | 368 | 0.68 | 2.11 | 9.99 | 99% | 0.50 | 1.64 | 3.70 | 139.2 → 131.3 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.64 | 1.98 | 10.17 | 99% | 0.52 | 1.58 | 3.23 | 138.6 → 130.7 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.36 | 1.47 | 8.08 | 100% | 0.38 | 1.24 | 2.54 | 75.0 → 71.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.151 |
| ArrangingLayoutable CachedLineNumberMargin | 0.039 |
| ArrangingLayoutable TextView | 0.010 |
| AttachingStyle - | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.006 |
| ArrangingLayoutable ScrollingTextLayer | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable NumberDrawing | 0.004 |
| ArrangingLayoutable RepeatButton | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.49 | 1.65 | 1.83 | 100% | 0.39 | 1.41 | 3.59 | 81.5 → 102.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2239 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 6 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.78 | 3.18 | 9.25 | 99% | 0.66 | 2.31 | 4.00 | 135.5 → 132.1 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.01 | 2.47 | 15.45 | 98% | 0.58 | 1.92 | 3.76 | 346.4 → 339.6 | 8 | 1.1 / 4 |

