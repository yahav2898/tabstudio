# Twenty long tabs

- The sample with 20 more C# files of about 1,500 lines each, in the IDE's window at 1280 × 800.
- The tab in view colored by Roslyn 4.9 s after the first was opened.

| Action | Cost |
| --- | --- |
| Opening a tab, to its first frame | median 187.2 ms, worst 891.6 ms, 53 KB each |
| Switching to another tab, to its first frame | median 12.2 ms, worst 79.2 ms, 360 KB each |
| A keystroke with 21 tabs open | median 10.2 ms, worst 38.2 ms, 311 KB each |
| Closing a tab | median 15.1 ms, worst 40.9 ms, 447 KB each |

After closing them: 2 of 21 documents and 2 of 21 editors still in memory.

The UI thread: 23 over 50 ms (0 over 250 ms, 0 over 1 s), 1.8 s in all, longest 125 ms during opening the tabs, 1.1 s of it garbage collection; answer p50 2.2 ms, p95 33.8 ms, p99 82.9 ms over 593 questions in 17 s.

| Stall | Of it, GC | At | During |
| --- | --- | --- | --- |
| 125 ms | 75 ms | 2.1 s | opening the tabs |
| 108 ms | 81 ms | 1.7 s | opening the tabs |
| 104 ms | 62 ms | 3.9 s | opening the tabs |
| 99 ms | 71 ms | 4.3 s | opening the tabs |
| 87 ms | 79 ms | 2.4 s | opening the tabs |
| 83 ms | 60 ms | 4.5 s | opening the tabs |
| 82 ms | 69 ms | 4.8 s | opening the tabs |
| 81 ms | 64 ms | 1.1 s | opening the tabs |
| 80 ms | 14 ms | 0.5 s | opening the tabs |
| 77 ms | 13 ms | 0.3 s | opening the tabs |
| 75 ms | 53 ms | 2.5 s | opening the tabs |
| 75 ms | 38 ms | 0.7 s | opening the tabs |
