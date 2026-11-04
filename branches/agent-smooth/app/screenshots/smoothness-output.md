# A program printing 20,000 lines as fast as it can

- The Output panel shown in a 1280 × 400 window, keeping its last 1,000,000 characters; a frame asked for every 16 ms meanwhile.

| Appending | Every line shown after | Frames drawn meanwhile | The UI thread meanwhile |
| --- | --- | --- | --- |
| As before 0.21.11: everything queued in one job at normal priority, one insertion per write | 58.79 s | 2 | 1 over 50 ms (1 over 250 ms, 1 over 1 s), 58.8 s in all, longest 58767 ms during the end of the run, 1.4 s of it garbage collection; answer p50 22.2 ms, p95 58767.2 ms, p99 58767.2 ms over 2 questions in 59 s |
| In batches of 64 KB behind input and drawing | 0.20 s | 8 | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 2.2 ms, p95 11.8 ms, p99 16.1 ms over 22 questions in 0 s |
