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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.85 | 2.21 | 11.74 | 99% | 0.51 | 1.74 | 4.42 | 116.0 → 117.4 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.90 | 2.27 | 13.63 | 99% | 0.64 | 1.70 | 4.24 | 154.5 → 152.2 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.17 | 4.45 | 75.90 | 99% | 2.56 | 2.71 | 4.47 | 190.8 → 175.4 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.78 | 2.08 | 12.77 | 99% | 0.56 | 1.64 | 3.89 | 133.8 → 130.9 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.76 | 2.08 | 11.40 | 99% | 0.59 | 1.62 | 4.06 | 110.2 → 103.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.78 | 2.13 | 10.88 | 99% | 0.56 | 1.64 | 4.05 | 136.4 → 128.2 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.18 | 15.10 | 99% | 0.57 | 1.63 | 4.06 | 136.5 → 128.3 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.90 | 3.39 | 14.26 | 100% | 0.72 | 2.52 | 4.27 | 135.7 → 127.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.71 | 1.96 | 9.10 | 99% | 0.51 | 1.57 | 3.80 | 112.9 → 107.8 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.78 | 2.13 | 11.40 | 99% | 0.54 | 1.66 | 3.91 | 138.8 → 131.2 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.77 | 2.07 | 12.16 | 99% | 0.58 | 1.60 | 3.54 | 138.6 → 130.4 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 1.77 | 7.10 | 100% | 0.53 | 1.34 | 3.01 | 73.9 → 70.4 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.296 |
| ArrangingLayoutable ScrollingTextLayer | 0.057 |
| AttachingStyle - | 0.032 |
| ArrangingLayoutable TextView | 0.019 |
| ArrangingLayoutable Track | 0.018 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.90 | 3.13 | 11.03 | 98% | 0.47 | 2.57 | 4.41 | 80.7 → 102.7 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 268 frames at 120 Hz; the text stood still on 0 of them, 176 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.85 | 2.25 | 14.94 | 99% | 0.57 | 1.73 | 4.11 | 134.6 → 131.4 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.14 | 2.44 | 16.38 | 98% | 0.61 | 1.93 | 4.04 | 347.8 → 341.2 | 8 | 1.1 / 4 |

