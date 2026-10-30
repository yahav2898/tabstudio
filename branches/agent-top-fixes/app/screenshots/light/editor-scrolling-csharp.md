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
| drag, 15 dip per frame (900 dip/s at 60 Hz) | 300 | 0.58 | 2.33 | 2.66 | 100% | 0.38 | 2.01 | 4.45 | 86.0 → 86.9 | 0 | 0.7 / 1 |
| 4 flings at 5000 dip/s | 436 | 0.76 | 2.58 | 48.08 | 100% | 0.63 | 2.09 | 4.58 | 123.4 → 121.1 | 1 | 1.4 / 4 |
| 2 flings at 8000 dip/s | 308 | 0.85 | 3.06 | 5.18 | 100% | 1.15 | 2.24 | 4.73 | 158.5 → 143.0 | 1 | 2.3 / 29 |

## Without one part of the editor at a time

The same drag and two 5000 dip/s flings, with one part of the editor taken out.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| everything | 368 | 0.67 | 2.63 | 20.04 | 100% | 0.58 | 2.16 | 4.64 | 103.3 → 100.3 | 1 | 1.1 / 4 |
| without line numbers | 368 | 0.56 | 2.48 | 3.27 | 100% | 0.58 | 2.04 | 4.50 | 80.5 → 73.3 | 0 | 1.2 / 30 |
| without the folding margin | 368 | 0.60 | 2.32 | 50.06 | 100% | 0.53 | 1.96 | 4.43 | 105.8 → 97.5 | 1 | 1.2 / 30 |
| without the breakpoint margin | 368 | 0.63 | 2.36 | 4.21 | 100% | 0.55 | 1.99 | 4.42 | 105.7 → 97.4 | 0 | 1.2 / 30 |
| without the Live Unit Testing margin | 368 | 0.67 | 3.95 | 49.94 | 100% | 1.00 | 2.47 | 4.66 | 105.0 → 96.8 | 2 | 1.2 / 30 |
| without coloring | 368 | 0.60 | 2.36 | 10.37 | 100% | 0.48 | 2.00 | 4.31 | 83.4 → 78.0 | 0 | 1.2 / 30 |
| without squiggles | 368 | 0.65 | 2.47 | 3.46 | 100% | 0.57 | 2.02 | 4.53 | 108.3 → 100.3 | 1 | 1.2 / 30 |
| without the floating touch bar | 368 | 0.65 | 2.49 | 2.88 | 100% | 0.53 | 2.06 | 4.02 | 108.4 → 100.2 | 0 | 1.2 / 30 |
| bare editor: no margins, coloring, squiggles or touch bar | 368 | 0.39 | 2.12 | 3.43 | 100% | 0.84 | 1.78 | 3.45 | 46.1 → 42.2 | 0 | 1.2 / 30 |

## Where layout time goes

Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).

Style attachments per frame: 51.5

| Activity and control | ms per frame |
|---|---|
| AttachingStyle - | 0.243 |
| MeasuringLayoutable TextView | 0.238 |
| ArrangingLayoutable NumberDrawing | 0.016 |
| ArrangingLayoutable Grid | 0.011 |
| ArrangingLayoutable TextView | 0.011 |
| ArrangingLayoutable LineDrawing | 0.009 |
| ArrangingLayoutable CachedLineNumberMargin | 0.008 |
| FindingResource - | 0.007 |
| ArrangingLayoutable Track | 0.007 |
| ArrangingLayoutable ScrollingTextLayer | 0.006 |
| ArrangingLayoutable RepeatButton | 0.003 |
| ArrangingLayoutable ScrollContentPresenter | 0.003 |
| RaisingRoutedEvent ScrollBar | 0.003 |
| MeasuringLayoutable CachedLineNumberMargin | 0.002 |
| ArrangingLayoutable Border | 0.002 |
| ArrangingLayoutable Thumb | 0.002 |
| MeasuringLayoutable LineDrawing | 0.001 |
| MeasuringLayoutable NumberDrawing | 0.001 |

## Scrolling with a finger

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| touch drag and flick (real touch input) | 46 | 0.56 | 2.44 | 4.32 | 100% | 0.46 | 1.94 | 4.54 | 50.1 → 72.0 | 0 | 0.5 / 2 |

- Drag: the text moved 1.000 dip per dip of finger movement (1.000 is one to one).
- Starting the drag did not move the caret and left the focus alone.
- Release at 6620 dip/s: glided 5173 dip in 2238 ms over 236 frames at 120 Hz; the text stood still on 0 of them, 261 started late.
- Android's OverScroller glides 5173 dip in 2233 ms from the same release.

## Before and after, in this run

The same drag and two 5000 dip/s flings with this editor, then with AvaloniaEdit's own text layer, line numbers, folding margin put back, as before. The colorizer, squiggle, breakpoint and Live Unit Testing changes are in both.

| Scroll | Frames | UI p50 ms | UI p95 ms | UI max ms | Frames < 8 ms | Layout p95 | Render p95 | Compositor p95 | KB/frame (1st → 2nd half) | GCs | Lines built/frame (mean / max) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| this editor | 368 | 0.65 | 2.43 | 3.36 | 100% | 0.53 | 2.05 | 4.56 | 104.2 → 100.9 | 0 | 1.1 / 4 |
| AvaloniaEdit's own parts (before) | 368 | 0.99 | 2.84 | 52.96 | 100% | 0.60 | 2.35 | 4.71 | 316.5 → 308.6 | 1 | 1.1 / 4 |

