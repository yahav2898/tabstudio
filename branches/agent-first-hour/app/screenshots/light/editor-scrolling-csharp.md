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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.48 | 2.74 | 14.28 | 100% | 0.37 | 2.47 | 3.94 | 84.4 → 85.2 | 1 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.67 | 3.06 | 16.14 | 99% | 0.60 | 2.54 | 4.07 | 121.3 → 121.8 | 4 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.76 | 3.10 | 15.98 | 99% | 0.95 | 2.58 | 4.19 | 156.7 → 141.8 | 3 | 2.2 / 28 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.55 | 2.84 | 17.41 | 99% | 0.50 | 2.50 | 3.98 | 101.6 → 98.8 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.55 | 3.12 | 29.27 | 99% | 0.67 | 2.55 | 4.18 | 79.8 → 73.0 | 3 | 1.2 / 29 |
| without the folding margin | 368 | 0.54 | 2.84 | 21.05 | 99% | 0.52 | 2.44 | 4.03 | 104.3 → 96.0 | 2 | 1.2 / 29 |
| without the breakpoint margin | 368 | 0.65 | 3.10 | 14.84 | 100% | 0.57 | 2.60 | 4.33 | 104.2 → 96.0 | 2 | 1.2 / 29 |
| without the Live Unit Testing margin | 368 | 0.55 | 2.74 | 15.77 | 99% | 0.52 | 2.42 | 3.91 | 103.5 → 95.3 | 2 | 1.2 / 29 |
| without coloring | 368 | 0.52 | 2.75 | 15.29 | 99% | 0.45 | 2.43 | 3.83 | 82.3 → 76.9 | 2 | 1.2 / 29 |
| without squiggles | 368 | 0.63 | 3.37 | 19.02 | 99% | 0.63 | 2.64 | 4.28 | 106.7 → 98.8 | 4 | 1.2 / 29 |
| without the floating touch bar | 368 | 0.61 | 3.04 | 15.08 | 99% | 0.63 | 2.57 | 3.65 | 106.8 → 98.6 | 2 | 1.2 / 29 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.33 | 2.53 | 11.55 | 100% | 0.96 | 2.24 | 3.00 | 46.1 → 42.4 | 1 | 1.2 / 29 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 62.2

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.182 |
| AttachingStyle - | 0.113 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable CachedLineNumberMargin | 0.009 |
| FindingResource - | 0.008 |
| ArrangingLayoutable LineDrawing | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.005 |
| ArrangingLayoutable Track | 0.005 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.47 | 2.79 | 3.13 | 100% | 0.37 | 2.40 | 4.27 | 49.2 → 72.9 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2240 ms over 256 frames at 120 Hz; the text stood still on 0 of them, 255 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.73 | 3.35 | 101.51 | 99% | 1.55 | 2.70 | 4.51 | 102.7 → 99.4 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.00 | 3.45 | 20.00 | 98% | 0.64 | 2.88 | 4.26 | 307.6 → 299.8 | 7 | 1.1 / 4 |

