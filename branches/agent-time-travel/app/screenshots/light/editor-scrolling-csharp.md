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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.60 | 1.65 | 1.98 | 100% | 0.36 | 1.36 | 4.00 | 113.3 → 114.6 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 1.94 | 48.02 | 100% | 0.59 | 1.48 | 4.14 | 151.4 → 149.0 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 2.08 | 43.30 | 100% | 0.69 | 1.49 | 4.26 | 186.9 → 171.1 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.70 | 2.93 | 4.10 | 100% | 0.55 | 2.42 | 4.14 | 131.2 → 128.2 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.58 | 2.58 | 10.59 | 100% | 0.53 | 2.33 | 3.89 | 107.5 → 100.4 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.65 | 1.76 | 2.16 | 100% | 0.54 | 1.38 | 3.99 | 133.4 → 125.2 | 0 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.68 | 1.83 | 42.20 | 100% | 0.54 | 1.44 | 4.10 | 133.3 → 125.2 | 1 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 1.89 | 46.34 | 100% | 0.53 | 1.44 | 3.98 | 132.5 → 124.5 | 1 | 1.2 / 30 |
| without coloring | 368 | 0.65 | 3.36 | 4.24 | 100% | 0.55 | 2.49 | 3.96 | 110.3 → 105.5 | 1 | 1.2 / 30 |
| without squiggles | 368 | 0.70 | 1.83 | 25.84 | 100% | 0.54 | 1.46 | 4.14 | 135.9 → 128.1 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 1.83 | 2.19 | 100% | 0.53 | 1.43 | 3.58 | 135.3 → 127.3 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.44 | 1.51 | 41.54 | 100% | 0.46 | 1.18 | 2.95 | 71.4 → 67.9 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 49.0

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.183 |
| AttachingStyle - | 0.013 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| FindingResource - | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable RepeatButton | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.003 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.60 | 1.68 | 1.77 | 100% | 0.40 | 1.36 | 4.29 | 78.2 → 99.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 260 frames at 120 Hz; the text stood still on 0 of them, 61 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.70 | 1.93 | 2.87 | 100% | 0.55 | 1.46 | 4.11 | 131.7 → 128.7 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.04 | 2.23 | 53.33 | 100% | 0.60 | 1.73 | 4.20 | 343.6 → 337.0 | 1 | 1.1 / 4 |

