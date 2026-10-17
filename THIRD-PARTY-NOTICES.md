# Third-party notices

The application's MIT license covers this project's code. Bundled dependencies retain their own licenses:

| Component | Version | License and notice |
| --- | --- | --- |
| Spectre.Console and Spectre.Console.Ansi | 0.57.2 | [MIT](licenses/Spectre.Console.LICENSE.txt), from the package's [source revision](https://github.com/spectreconsole/spectre.console/blob/bbbb5729dde27b58deee44f447a788eea46ee451/LICENSE.md) |
| Microsoft.Web.WebView2 | 1.0.4258.31 | [License](licenses/Microsoft.Web.WebView2.LICENSE.txt) and [third-party notices](licenses/Microsoft.Web.WebView2.NOTICE.txt), copied from the NuGet package |

The build script includes these files and the `licenses` directory in the Windows package. Keep them with redistributed builds. The .NET Desktop Runtime and WebView2 Runtime are installed separately and are not bundled in this framework-dependent application.

Test-only NuGet dependencies are not included in the application package. Their license metadata is available through NuGet.
