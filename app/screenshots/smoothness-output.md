# A program printing 20,000 lines as fast as it can

- The Output panel shown in a 1280 × 400 window, keeping its last 1,000,000 characters; a frame asked for every 16 ms meanwhile.

| Appending | Every line shown after | Frames drawn meanwhile | The UI thread meanwhile |
| --- | --- | --- | --- |
| As before 0.21.7: everything queued in one job at normal priority, one insertion per write | 12.64 s | 1 | 1 over 50 ms (1 over 250 ms, 1 over 1 s), 12.6 s in all, longest 12634 ms during the end of the run, 3.8 s of it garbage collection; answer p50 3.3 ms, p95 12634.3 ms, p99 12634.3 ms over 2 questions in 13 s |
| In batches of 64 KB behind input and drawing | 0.12 s | 6 | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 2.2 ms, p95 15.7 ms, p99 15.7 ms over 18 questions in 0 s |
