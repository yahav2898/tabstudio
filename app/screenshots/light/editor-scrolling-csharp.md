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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.04 | 11.22 | 99% | 0.37 | 1.77 | 4.06 | 117.2 → 118.6 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.81 | 2.45 | 12.38 | 99% | 0.57 | 1.97 | 4.44 | 154.9 → 152.7 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.06 | 4.90 | 17.09 | 99% | 2.59 | 4.02 | 4.82 | 190.8 → 175.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.66 | 2.16 | 12.35 | 99% | 0.50 | 1.79 | 4.26 | 134.9 → 132.1 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.05 | 15.49 | 99% | 0.49 | 1.71 | 4.26 | 111.6 → 104.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.04 | 15.79 | 99% | 0.48 | 1.67 | 4.08 | 136.8 → 129.1 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.74 | 2.95 | 11.63 | 99% | 0.59 | 1.99 | 4.42 | 137.0 → 129.4 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.68 | 2.49 | 10.87 | 100% | 0.52 | 1.84 | 4.21 | 136.5 → 128.2 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.56 | 1.98 | 10.67 | 99% | 0.40 | 1.67 | 3.93 | 114.6 → 109.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.64 | 2.06 | 12.29 | 99% | 0.48 | 1.70 | 4.14 | 139.5 → 131.8 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 2.03 | 12.80 | 99% | 0.49 | 1.67 | 3.56 | 139.2 → 131.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.51 | 2.64 | 9.53 | 99% | 0.59 | 1.75 | 3.45 | 75.9 → 72.9 | 3 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.211 |
| AttachingStyle - | 0.022 |
| ArrangingLayoutable ScrollingTextLayer | 0.017 |
| ArrangingLayoutable NumberDrawing | 0.015 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.52 | 2.02 | 2.46 | 100% | 0.47 | 1.63 | 7.05 | 82.7 → 104.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 25 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.64 | 2.08 | 12.20 | 99% | 0.46 | 1.72 | 4.15 | 135.3 → 132.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.15 | 4.79 | 20.31 | 98% | 0.85 | 4.33 | 4.52 | 347.6 → 341.2 | 9 | 1.1 / 4 |

