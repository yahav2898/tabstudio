# Scrolling a long C# file

- 2509 lines; 29 visible lines; viewport 755 × 614 dip.
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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.61 | 3.04 | 12.59 | 99% | 0.41 | 2.67 | 4.37 | 86.0 → 86.9 | 2 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.75 | 3.23 | 16.40 | 99% | 0.66 | 2.70 | 4.41 | 123.4 → 121.1 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.88 | 3.77 | 19.45 | 99% | 1.76 | 2.87 | 4.90 | 158.5 → 143.0 | 4 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.72 | 3.39 | 12.52 | 99% | 0.62 | 2.77 | 5.02 | 103.3 → 100.3 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.53 | 2.94 | 10.58 | 100% | 0.52 | 2.51 | 4.21 | 80.6 → 73.3 | 1 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.98 | 10.40 | 100% | 0.53 | 2.59 | 4.23 | 105.8 → 97.5 | 2 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.64 | 3.18 | 15.27 | 99% | 0.59 | 2.70 | 4.40 | 105.7 → 97.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.66 | 3.35 | 15.33 | 100% | 0.65 | 2.75 | 4.84 | 105.1 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.54 | 2.83 | 12.44 | 100% | 0.43 | 2.52 | 3.97 | 83.4 → 78.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.63 | 2.92 | 15.65 | 99% | 0.53 | 2.57 | 4.27 | 108.3 → 100.3 | 2 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.61 | 3.05 | 16.10 | 99% | 0.58 | 2.63 | 3.84 | 108.4 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.42 | 2.94 | 18.41 | 100% | 1.26 | 2.48 | 3.56 | 46.1 → 42.2 | 2 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.186 |
| ArrangingLayoutable CachedLineNumberMargin | 0.057 |
| AttachingStyle - | 0.012 |
| ArrangingLayoutable TextView | 0.012 |
| FindingResource - | 0.009 |
| ArrangingLayoutable LineDrawing | 0.008 |
| ArrangingLayoutable ScrollingTextLayer | 0.007 |
| ArrangingLayoutable Track | 0.006 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.002 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| RaisingRoutedEvent ScrollBar | 0.002 |
| MeasuringLayoutable LineDrawing | 0.002 |
| ArrangingLayoutable Border | 0.001 |
| ArrangingLayoutable Thumb | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.56 | 3.13 | 3.64 | 100% | 0.44 | 2.66 | 4.44 | 50.2 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2242 ms over 253 frames at 120 Hz; the text stood still on 0 of them, 260 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.63 | 2.97 | 15.20 | 99% | 0.49 | 2.58 | 4.29 | 104.1 → 100.9 | 2 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.27 | 6.02 | 22.93 | 98% | 2.67 | 5.10 | 5.24 | 316.6 → 308.6 | 8 | 1.1 / 4 |

