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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 1.10 | 2.86 | 11.38 | 99% | 0.54 | 2.36 | 10.45 | 134.3 → 134.4 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.49 | 4.30 | 16.07 | 99% | 0.79 | 2.70 | 10.79 | 174.5 → 171.5 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.49 | 3.22 | 17.14 | 99% | 0.92 | 2.41 | 10.53 | 209.2 → 192.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 1.09 | 2.76 | 12.09 | 99% | 0.65 | 2.17 | 10.23 | 150.9 → 148.1 | 4 | 1.1 / 4 |
| without line numbers | 368 | 1.02 | 4.38 | 11.01 | 99% | 0.70 | 2.32 | 10.31 | 128.1 → 120.8 | 3 | 1.2 / 30 |
| without the folding margin | 368 | 1.00 | 2.62 | 12.94 | 99% | 0.65 | 2.11 | 9.89 | 152.9 → 144.8 | 4 | 1.2 / 30 |
| without the breakpoint margin | 368 | 1.06 | 2.70 | 12.83 | 99% | 0.64 | 2.11 | 10.02 | 152.3 → 144.8 | 4 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 1.13 | 4.39 | 78.03 | 99% | 0.73 | 2.71 | 10.34 | 153.9 → 144.1 | 4 | 1.2 / 30 |
| without coloring | 368 | 1.05 | 2.53 | 10.66 | 99% | 0.55 | 2.02 | 9.60 | 129.6 → 125.0 | 3 | 1.2 / 30 |
| without squiggles | 368 | 1.09 | 2.80 | 13.08 | 99% | 0.65 | 2.22 | 10.20 | 155.4 → 148.1 | 4 | 1.2 / 30 |
| without the floating touch bar | 368 | 1.14 | 4.61 | 12.81 | 99% | 0.68 | 2.71 | 9.56 | 156.1 → 147.1 | 4 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.84 | 2.42 | 8.33 | 100% | 0.59 | 1.88 | 8.43 | 91.4 → 87.9 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 50.7

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.265 |
| ArrangingLayoutable TextView | 0.022 |
| FindingResource - | 0.014 |
| AttachingStyle - | 0.014 |
| ArrangingLayoutable LineDrawing | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.011 |
| ArrangingLayoutable Track | 0.009 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| RaisingRoutedEvent ScrollBar | 0.004 |
| ArrangingLayoutable RepeatButton | 0.004 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.93 | 2.63 | 3.01 | 100% | 0.54 | 2.11 | 10.49 | 97.4 → 119.1 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 182 frames at 120 Hz; the text stood still on 0 of them, 215 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 1.07 | 2.68 | 13.07 | 99% | 0.66 | 2.10 | 10.06 | 150.9 → 148.2 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.79 | 3.57 | 17.17 | 98% | 0.83 | 2.67 | 10.51 | 366.7 → 361.0 | 9 | 1.1 / 4 |

