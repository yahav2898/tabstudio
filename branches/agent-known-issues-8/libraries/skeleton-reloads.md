# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 906 ms | 5.6 ms | 9.8 ms |
| Memory pressure | 19 ms | 0.1 ms | 8.0 ms |
