# Nested enum metadata collision in a generated C# JSON context

OpenAPI Generator's C# `generichost` client with `useSourceGeneration=true`
emits `SYSLIB1031` when a model references two models with identically named
nested enums. This repro uses an `Asset` object with `ImageAsset` and
`DocumentAsset` properties. Both referenced models have a nested `TypeEnum`.
The generated `AssetSerializationContext` omits metadata for one enum.

The warning fails builds configured to treat it as an error. Direct use of the
root context cannot serialize the missing enum. However, combining the generated
model contexts resolves both enums, as the runner verifies without reflection.
The stock client's configuration combines these contexts. This repro does not
claim that normal model serialization with the generated converters fails.

## Reproduce

Requires Bash, .NET SDK 10, and Docker with a running Linux-container daemon. Run from
the repository root:

```sh
bash generate.sh
dotnet run --project Repro
```

The script validates `schema.yaml`, then generates an unmodified client using
the official 7.26.0 image. There are no custom templates, post-processing, server,
or schema exporter. Validation reports no errors and three `Unused model`
recommendations because `paths` is empty. Validation and generation both exit `0`.

The .NET build succeeds with one `SYSLIB1031` warning. The runner exits `1`.
Selected output:

```text
CONTROL ReproClient.Model.ImageAsset+TypeEnum: own context present; JSON=1
COMBINED ReproClient.Model.ImageAsset+TypeEnum: present; JSON=1
ROOT ReproClient.Model.ImageAsset+TypeEnum: present; JSON=1
CONTROL ReproClient.Model.DocumentAsset+TypeEnum: own context present; JSON=1
COMBINED ReproClient.Model.DocumentAsset+TypeEnum: present; JSON=1
ROOT ReproClient.Model.DocumentAsset+TypeEnum: MISSING; serialization throws InvalidOperationException
RESULT BUG REPRODUCED: 1/2 nested enum contracts missing; individual and combined context controls passed.
```

The runner also prints the exception message and environment. Enums are serialized
as standalone values to exercise their metadata. Numeric enum output (`1`) is
incidental to this test. Reflection-based serialization is disabled.

Exit codes: `0` means both root-context entries and all controls work;
`1` with `RESULT BUG REPRODUCED` means metadata is missing and the controls passed;
`2` reports a control failure or inconsistent metadata/output. Other failures,
such as a failed build, can also return a nonzero code; check the result line.
Unexpected exceptions are not caught.

To test 7.25.0, run in a separate fresh copy of the source:

```sh
bash generate.sh 7.25.0
dotnet run --project Repro
```

Generated code lives in ignored `generated/`. It is never patched by the repro.
The container runs with the caller's UID/GID so generated files remain writable
when using native Linux Docker. The verified runs below used macOS Docker Desktop.

## Verified environment

Verified on 2026-10-06 from fresh directories without previous generated or build
output. Both versions produced the same warning and runtime result.

| Component | Version |
|---|---|
| OpenAPI Generator | 7.25.0 (`ef964b0`) and 7.26.0 (`fba9e36`) |
| Latest release checked | 7.26.0, released 2026-10-06 |
| .NET SDK / runtime | 10.0.401 / 10.0.12 |
| Loaded System.Text.Json | Bundled 10.0.12, not a separately referenced NuGet package |
| Host | macOS 26.6.2, ARM64 |
| Docker | 29.8.0, Linux ARM64 containers |
| Generated client's direct packages | Microsoft.Extensions.Hosting, Microsoft.Extensions.Http, Microsoft.Extensions.Http.Polly, Microsoft.Net.Http.Headers: all 10.0.1 |

The runner prints runtime, architecture, and the loaded System.Text.Json assembly
version and location. Use `dotnet --info` and
`dotnet list generated/src/ReproClient/ReproClient.csproj package --include-transitive`
for full environment information.

In a separate fresh generated copy, `dotnet build Repro -warnaserror:SYSLIB1031`
failed with exit code `1` and the diagnostic promoted to an error.

The current master commit `609b95d844d80d067026d414ac24472c25ba88f2` was inspected,
not built. Its generator source and templates are unchanged from 7.26.0; the
intervening commit prepares version 7.27.0. This is a source comparison, not a
claim of executing a master build.

## Cause and separately verified correction

The stock [7.26.0 context template](https://github.com/OpenAPITools/openapi-generator/blob/v7.26.0/modules/openapi-generator/src/main/resources/csharp/libraries/generichost/SourceGenerationContext.mustache)
only annotates the root model. System.Text.Json discovers both nested enums but
assigns both the metadata name `TypeEnum`. Its diagnostic says it generates
source only for the first type detected.

In a separate experimental copy, adding these attributes to the existing
`AssetSerializationContext` declaration removed the warning and made the same
runner exit `0`:

```csharp
[JsonSerializable(typeof(ImageAsset.TypeEnum), TypeInfoPropertyName = "ImageAssetTypeEnum")]
[JsonSerializable(typeof(DocumentAsset.TypeEnum), TypeInfoPropertyName = "DocumentAssetTypeEnum")]
```

That change is not included in the failing repro. The generator should emit
unambiguous metadata names for reachable nested enums. See Microsoft's
[SYSLIB1031 documentation](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/syslib1031).
