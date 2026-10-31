# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 917 ms | 2.0 ms | 86.6 ms |
| Memory pressure | 22 ms | 0.1 ms | 11.6 ms |
