# Build quality

[Directory.Build.props](../Directory.Build.props) applies to the Android app and both host-check projects. It enables nullable reference analysis, implicit usings, SDK analyzers, deterministic compilation and warnings as errors. NuGet auditing is explicitly enabled for direct and transitive dependencies; restore warnings, including unavailable audit data, must be resolved rather than suppressed.

The analyzer baseline is pinned to .NET 10.0. Security, Reliability, Usage, Performance, Maintainability, Interoperability and Globalization use the SDK recommended rules. Design and Naming retain the .NET 10 default set, avoiding cosmetic API renames. Code-style suggestions remain editor guidance; `EnforceCodeStyleInBuild=false` does not turn every IDE style preference into a build error. No rule IDs are suppressed. Compiler/analyzer and NuGet warnings fail their builds/restores; this does not claim every arbitrary MSBuild task warning is fatal.

[.editorconfig](../.editorconfig) defines UTF-8, LF, indentation and scoped C# suggestions. Existing unrelated files are not reformatted. SDK upgrades require a deliberate policy review; pinned analyzer levels avoid silently adopting a future SDK's recommended set. Deterministic compilation is not a promise of byte-identical signed APKs.

## Verification

Run the existing SessionChecks and GhostChecks projects, then build the MAUI Android app using [the supported toolchain](android.md). Required repository CI remains unchanged. Keep restore auditing online and do not introduce `NoWarn` or ignore failed restores. The initial local pilot used SDK 10.0.100; its effective `EditorConfigFiles` contained the default global/Design/Naming warn-as-error configs and all seven recommended category configs. The platform annotation on notification permission matches the existing Android 33 caller guard; negative-duration checks use equivalent `ThrowIf` guards. Constructor rejection tests explicitly discard their constructed result.

To inspect actual selected configuration, invoke the SDK targets `AddGlobalAnalyzerConfigForPackage_MicrosoftCodeAnalysisNetAnalyzers` and its category-suffixed targets with `dotnet msbuild`, then request `-getItem:EditorConfigFiles`. Declared properties alone do not prove selected configs. Single-file publishing is not enabled by this policy.

Sources: [MSBuild analysis properties](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props), [analyzer configuration](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options), [NuGet auditing](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).
