# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 707 ms | 17.8 ms | 55.2 ms |
| Memory pressure | 19 ms | 0.2 ms | 7.2 ms |
