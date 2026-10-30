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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.56 | 3.20 | 15.89 | 99% | 0.41 | 2.86 | 4.41 | 86.0 → 86.9 | 3 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 1.02 | 4.11 | 18.81 | 99% | 2.04 | 3.22 | 5.20 | 123.5 → 121.2 | 3 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 1.03 | 3.86 | 20.64 | 99% | 0.90 | 3.12 | 4.91 | 158.6 → 143.0 | 3 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.86 | 3.65 | 17.21 | 99% | 0.66 | 3.07 | 4.74 | 103.3 → 100.4 | 2 | 1.1 / 4 |
| without line numbers | 368 | 0.70 | 3.54 | 17.06 | 99% | 0.66 | 2.93 | 4.60 | 80.6 → 73.4 | 2 | 1.2 / 30 |
| without the folding margin | 368 | 1.09 | 4.01 | 19.61 | 99% | 0.91 | 3.27 | 5.14 | 105.9 → 97.6 | 3 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.82 | 3.60 | 18.94 | 99% | 0.64 | 3.08 | 4.64 | 105.8 → 97.5 | 3 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.89 | 3.76 | 18.94 | 99% | 0.69 | 3.15 | 4.74 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.69 | 3.37 | 14.21 | 100% | 0.52 | 2.98 | 4.32 | 83.4 → 78.0 | 2 | 1.2 / 30 |
| without squiggles | 368 | 0.89 | 3.93 | 18.91 | 99% | 0.73 | 3.25 | 4.85 | 108.3 → 100.4 | 3 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.82 | 3.74 | 18.16 | 99% | 0.66 | 3.11 | 4.31 | 108.5 → 100.2 | 3 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.53 | 3.14 | 12.86 | 100% | 1.31 | 2.74 | 3.43 | 46.1 → 42.2 | 1 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| MeasuringLayoutable TextView | 0.256 |
| AttachingStyle - | 0.067 |
| ArrangingLayoutable TextView | 0.019 |
| FindingResource - | 0.013 |
| ArrangingLayoutable LineDrawing | 0.010 |
| ArrangingLayoutable CachedLineNumberMargin | 0.010 |
| ArrangingLayoutable ScrollingTextLayer | 0.008 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable NumberDrawing | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |
| MeasuringLayoutable Grid | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.74 | 3.50 | 3.64 | 100% | 0.50 | 2.95 | 4.83 | 50.1 → 72.1 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2246 ms over 196 frames at 120 Hz; the text stood still on 0 of them, 228 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.93 | 3.87 | 18.22 | 99% | 0.75 | 3.15 | 4.86 | 104.2 → 100.9 | 3 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 1.21 | 4.01 | 23.17 | 98% | 0.75 | 3.36 | 4.60 | 316.7 → 308.7 | 7 | 1.1 / 4 |

