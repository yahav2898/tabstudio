# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 450 ms | 2.5 ms | 8.3 ms |
| Memory pressure | 23 ms | 0.1 ms | 12.5 ms |
