# Open an experience from a saved account

In Accounts, select an account and press P. Enter the numeric place ID from `/games/<place ID>/...`, then choose Check account and open. The fallback menu offers Open experience. The CLI equivalent is:

~~~powershell
.\.artifacts\publish\myka.exe accounts play --account LOCAL_ACCOUNT_ID --place PLACE_ID
~~~

The local account ID comes from `accounts list`; it is not the Roblox user ID. This action needs an interactive terminal and WebView2 Runtime.

The experience window opens the saved browser profile while holding the account-operation lock. It checks the official authenticated-user endpoint against the saved numeric ID. A missing profile, expired login or mismatched user stops the attempt. Close the window and use Sign in again when needed. This flow does not replace saved metadata or its last-verified timestamp.

After that check, the browser opens only the chosen `https://www.roblox.com/games/<place ID>` page and its slug. Click Play there. A desktop request must use `roblox-player:`, come from the allowed Roblox origin while the chosen page is loaded, and carry WebView2's user-gesture indication. The app permits one request and leaves WebView2 in charge of its external-app prompt and Windows handoff.

The sign-in window continues to cancel external protocols. Both windows block downloads, extra windows, permissions, certificate errors, host objects and web messages, with password autosave disabled. The launcher does not construct tickets, inspect cookies, accept arbitrary URLs or run discovered executables.

Desktop handoff requested means the browser was permitted to offer the launch. Its prompt may still be cancelled, Windows may lack a handler, or Player may fail. The app does not verify a running process or which account joined. Check Player before retrying. Cancellation or browser failure after a request cannot retract it, so an uncertain handoff is never replayed automatically.

No real Roblox login or Player handoff has been exercised by the agent. Current website redirects, challenges, user-gesture propagation and WebView2 compatibility still need a manual check. This uses the current Windows device and network; separate browser profiles do not establish separate native clients or public addresses. Follow the [environment architecture](architecture.md) and [one-environment trial](environment-preflight.md) for that requirement.

WebView2's [external URI event specification](https://github.com/MicrosoftEdge/WebView2Feedback/blob/main/specs/LaunchingExternalUriScheme.md) documents an unsuccessful NavigationCompleted even when a handoff is permitted. The implementation tracks that navigation separately from the experience-page load.
