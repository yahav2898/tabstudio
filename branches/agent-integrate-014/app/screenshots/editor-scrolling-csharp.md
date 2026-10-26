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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.69 | 2.39 | 11.43 | 100% | 0.41 | 2.02 | 4.50 | 113.1 → 114.6 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.07 | 3.59 | 14.24 | 99% | 0.77 | 2.88 | 5.12 | 151.9 → 150.1 | 5 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.99 | 3.17 | 14.45 | 99% | 0.76 | 2.54 | 4.69 | 187.1 → 171.5 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.82 | 2.80 | 12.90 | 99% | 0.56 | 2.45 | 4.54 | 131.0 → 128.3 | 3 | 1.1 / 4 |
| without line numbers | 368 | 0.64 | 2.26 | 10.70 | 99% | 0.55 | 1.81 | 4.30 | 108.2 → 101.2 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 0.86 | 3.29 | 73.24 | 99% | 0.65 | 2.62 | 4.85 | 133.7 → 125.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.79 | 2.34 | 11.87 | 99% | 0.57 | 1.88 | 4.47 | 133.3 → 125.2 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.76 | 2.37 | 12.54 | 99% | 0.59 | 1.90 | 4.46 | 132.5 → 124.5 | 3 | 1.2 / 30 |
| without coloring | 368 | 0.74 | 2.35 | 10.35 | 100% | 0.49 | 1.89 | 4.40 | 110.5 → 105.5 | 3 | 1.2 / 30 |
| without squiggles | 368 | 0.87 | 2.87 | 14.24 | 99% | 0.61 | 2.08 | 4.79 | 136.5 → 128.1 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.75 | 2.27 | 12.43 | 99% | 0.52 | 1.79 | 3.90 | 136.2 → 127.8 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.49 | 1.86 | 2.36 | 100% | 0.47 | 1.54 | 3.32 | 71.8 → 68.3 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.193 |
| ArrangingLayoutable TextView | 0.015 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
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
| touch drag and flick (real touch input) | 46 | 0.68 | 2.22 | 12.06 | 98% | 0.48 | 1.85 | 4.83 | 78.4 → 99.9 | 1 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 243 frames at 120 Hz; the text stood still on 0 of them, 229 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.87 | 2.56 | 11.87 | 99% | 0.64 | 2.04 | 4.77 | 131.6 → 128.6 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.26 | 2.94 | 18.05 | 98% | 0.66 | 2.31 | 4.66 | 343.9 → 337.7 | 8 | 1.1 / 4 |

