# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 1080 ms | 12.7 ms | 9.0 ms |
| Memory pressure | 50 ms | 0.1 ms | 20.7 ms |
