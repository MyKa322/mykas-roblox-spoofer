# README media capture

This Windows-only tool generates the documentation PNGs and GIFs from the application's renderer and native System Identity controls. Accounts and snapshots are fictional fixtures. It never opens the account store, reads local identifiers, signs in, launches Player or applies system operations.

From the repository root with .NET SDK 10 installed:

~~~powershell
dotnet run --project tools/ReadmeMedia -c Release -- docs/media
~~~

The terminal PNG uses the real terminal layout, logo renderer and account page, rasterized with Consolas. The native PNGs render the real controls after capturing two synthetic snapshots. GIFs use the Windows imaging codec, an adaptive palette and 900-pixel maximum width; the PNGs retain their full resolution. A small writer adds explicit frame delays and infinite looping because the Windows codec omits those control blocks. The tool uses WPF only for image encoding. The application itself remains a WinForms host.

The output directory is required. The command replaces the six named media files there. Inspect the images before publishing them; screenshots do not validate real login, device changes or recovery.
