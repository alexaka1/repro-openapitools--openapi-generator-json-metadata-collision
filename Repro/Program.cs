using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ReproClient.Model;

Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"System.Text.Json: {typeof(JsonSerializer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
Console.WriteLine($"System.Text.Json location: {typeof(JsonSerializer).Assembly.Location}");
Console.WriteLine($"Reflection enabled: {JsonSerializer.IsReflectionEnabledByDefault}");

(Type Type, object Value, JsonSerializerContext Context)[] cases =
[
    (typeof(ImageAsset.TypeEnum), ImageAsset.TypeEnum.Image, ImageAssetSerializationContext.Default),
    (typeof(DocumentAsset.TypeEnum), DocumentAsset.TypeEnum.Document, DocumentAssetSerializationContext.Default)
];

var combinedOptions = new JsonSerializerOptions
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine(
        AssetSerializationContext.Default,
        DocumentAssetSerializationContext.Default,
        ImageAssetSerializationContext.Default)
};
var missing = 0;
foreach (var (type, value, context) in cases)
{
    if (context.GetTypeInfo(type) is null)
    {
        Console.WriteLine($"CONTROL FAILED: {type.FullName} is missing from its own model context.");
        return 2;
    }

    var expectedJson = JsonSerializer.Serialize(value, type, context);
    Console.WriteLine($"CONTROL {type.FullName}: own context present; JSON={expectedJson}");
    if (JsonSerializer.Serialize(value, type, combinedOptions) != expectedJson)
    {
        Console.WriteLine($"CONTROL FAILED: combined contexts serialize {type.FullName} differently.");
        return 2;
    }
    Console.WriteLine($"COMBINED {type.FullName}: present; JSON={expectedJson}");
    var metadata = AssetSerializationContext.Default.GetTypeInfo(type);
    try
    {
        var actualJson = JsonSerializer.Serialize(value, type, AssetSerializationContext.Default);
        if (metadata?.Type != type || actualJson != expectedJson)
        {
            Console.WriteLine($"UNEXPECTED RESULT: metadata or JSON differs for {type.FullName}.");
            return 2;
        }
        Console.WriteLine($"ROOT {type.FullName}: present; JSON={actualJson}");
    }
    catch (InvalidOperationException exception) when (metadata is null)
    {
        missing++;
        Console.WriteLine(exception.Message);
        Console.WriteLine($"ROOT {type.FullName}: MISSING; serialization throws InvalidOperationException");
    }
}

Console.WriteLine(missing == 0
    ? "RESULT PASS: both nested enum contracts are available in the root context."
    : $"RESULT BUG REPRODUCED: {missing}/{cases.Length} nested enum contracts missing; individual and combined context controls passed.");
return missing == 0 ? 0 : 1;
