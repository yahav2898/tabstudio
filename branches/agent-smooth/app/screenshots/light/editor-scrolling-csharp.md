# Scrolling a long C# file

- 2509 lines; 28 visible lines; viewport 755 × 586 dip.
- 17724 classified spans, 4 diagnostics, 374 folds, 8 breakpoints, 1184 Live Unit Testing markers.
- Margins: BreakpointMargin, CachedLineNumberMargin, LiveTestingMargin, Line, DrawnFoldingMargin.
- Line transformers: SelectionColorizer, ClassificationColorizer, LineCounter.
- Background renderers: ReferenceHighlightRenderer, CurrentLineHighlightRenderer, ColumnRulerRenderer, DiagnosticRenderer, DebugLineRenderer, SnippetTabStopRenderer, BracketHighlightRenderer.
- Text view layers: SelectionLayer, ScrollingTextLayer, CaretLayer.
- Element generators: FoldingElementGenerator, SingleCharacterElementGenerator, BidiControlGenerator, GhostTextGenerator.
- Gesture recognizers: ScrollGestureRecognizer on ScrollContentPresenter, TouchScrollGestureRecognizer on TextArea.
- 4 processors, Unix 7.0.0.1017, .NET 10.0.14.

Avalonia frame metrics: on.

## Frames

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.52 | 2.76 | 15.36 | 99% | 0.35 | 2.45 | 4.08 | 84.4 → 85.2 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.70 | 3.09 | 16.09 | 99% | 0.58 | 2.59 | 4.39 | 121.3 → 121.7 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.93 | 4.30 | 71.35 | 99% | 2.54 | 2.76 | 4.71 | 156.8 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.59 | 2.88 | 14.51 | 99% | 0.53 | 2.51 | 4.23 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.47 | 2.84 | 11.98 | 99% | 0.54 | 2.40 | 3.93 | 79.7 → 73.0 | 2 | 1.2 / 29 |
| without the folding margin | 368 | 0.64 | 3.21 | 14.39 | 99% | 0.59 | 2.64 | 4.43 | 104.3 → 96.1 | 3 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.58 | 2.96 | 14.25 | 99% | 0.56 | 2.54 | 4.15 | 104.1 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.57 | 2.88 | 17.07 | 99% | 0.54 | 2.53 | 4.04 | 103.5 → 95.3 | 3 | 1.2 / 29 |
| without coloring | 368 | 0.52 | 2.81 | 12.15 | 99% | 0.44 | 2.48 | 3.94 | 82.2 → 76.9 | 3 | 1.2 / 29 |
| without squiggles | 368 | 0.67 | 3.22 | 7.97 | 100% | 0.83 | 2.64 | 4.46 | 106.7 → 98.8 | 2 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.57 | 2.93 | 14.33 | 99% | 0.53 | 2.55 | 3.59 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.34 | 2.59 | 9.53 | 100% | 0.50 | 2.29 | 2.97 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.230 |
| AttachingStyle - | 0.017 |
| ArrangingLayoutable TextView | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.48 | 2.79 | 3.08 | 100% | 0.38 | 2.44 | 4.19 | 49.2 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2241 ms over 252 frames at 120 Hz; the text stood still on 0 of them, 263 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.59 | 2.92 | 11.95 | 99% | 0.53 | 2.54 | 4.14 | 102.6 → 99.3 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.19 | 5.57 | 23.89 | 98% | 0.86 | 4.98 | 4.62 | 307.5 → 299.9 | 8 | 1.1 / 4 |

