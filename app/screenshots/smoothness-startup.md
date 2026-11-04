# TabStudio's start

## This test process's own start (cold)

| Part | Took | At |
| --- | --- | --- |
| styles | 306 ms | 306 ms |
| services | 79 ms | 385 ms |
| theme | 38 ms | 423 ms |
| view models | 158 ms | 581 ms |

## Again, warm

- Building the service provider: 0 ms.
- Every service, created one at a time: 21 ms.
- The main view's first frame on the start screen: 58 ms.

| Service (with what it needs that was not created before it) | Took |
| --- | --- |
| SwarmMemberService | 18.5 ms |
| ExtensionsService | 1.9 ms |
| LanguageServerServices | 0.4 ms |
| ToolboxCatalog | 0.1 ms |
| LocalAIService | 0.1 ms |
| AppearanceService | 0.1 ms |
| CompilerServices | 0.0 ms |
| SwarmCoordinatorService | 0.0 ms |
| LinkViewModel | 0.0 ms |
| DesktopModeViewModel | 0.0 ms |
| GitHubAccount | 0.0 ms |
| StartViewModel | 0.0 ms |
| SwarmJoinViewModel | 0.0 ms |
| CrashLog | 0.0 ms |
| WindowsBridgeService | 0.0 ms |
| LiveService | 0.0 ms |
| IDatabaseSecretStore | 0.0 ms |
| LinkService | 0.0 ms |
| SwarmDeviceHost | 0.0 ms |
| IDeviceStatus | 0.0 ms |
