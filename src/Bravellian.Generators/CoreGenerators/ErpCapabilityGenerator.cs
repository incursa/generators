// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace Bravellian.Generators;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

public static class ErpCapabilityGenerator
{
    public static GeneratorParams? GetParams(XElement xml, IBvLogger? logger)
    {
        try
        {
            var erpCapabilityDefinitionsElement = xml;

            // If the element isn't directly an ErpCapabilityDefinitions, check if we need to get it as a child
            if (!string.Equals(xml.Name.LocalName, "ErpCapabilityDefinitions", StringComparison.OrdinalIgnoreCase))
            {
                // Try to find it as a direct child
                erpCapabilityDefinitionsElement = xml.Element("ErpCapabilityDefinitions");

                if (erpCapabilityDefinitionsElement == null)
                {
                    logger?.LogError("Failed to find ErpCapabilityDefinitions element in XML.");
                    return null;
                }
            }

            IReadOnlyDictionary<string, string> attributes = erpCapabilityDefinitionsElement.GetAttributeDict();
            var generatedCapabilitiesNamespace = attributes.TryGetValue("GeneratedCapabilitiesNamespace");
            var generatedInterfacesNamespace = attributes.TryGetValue("GeneratedInterfacesNamespace");

            if (string.IsNullOrEmpty(generatedCapabilitiesNamespace) || string.IsNullOrEmpty(generatedInterfacesNamespace))
            {
                logger?.LogError("Missing required attributes on ErpCapabilityDefinitions element: GeneratedCapabilitiesNamespace or GeneratedInterfacesNamespace.");
                return null;
            }

            // Parse capabilities section
            var capabilitiesElement = erpCapabilityDefinitionsElement.Element("Capabilities");
            if (capabilitiesElement == null)
            {
                logger?.LogError("Failed to find Capabilities element in XML.");
                return null;
            }

            var capabilities = new List<CapabilityDescriptor>();
            foreach (var capabilityElement in capabilitiesElement.Elements("Capability"))
            {
                var capAttrs = capabilityElement.GetAttributeDict();
                var name = capAttrs.TryGetValue("Name");
                var interfaceName = capAttrs.TryGetValue("Interface");
                var description = capAttrs.TryGetValue("Description") ?? string.Empty;

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(interfaceName))
                {
                    logger?.LogWarning($"Skipping capability with missing name or interface.");
                    continue;
                }

                capabilities.Add(new CapabilityDescriptor(name, interfaceName, description));
            }

            // Parse adapter profiles section
            var adapterProfilesElement = erpCapabilityDefinitionsElement.Element("AdapterProfiles");
            if (adapterProfilesElement == null)
            {
                logger?.LogError("Failed to find AdapterProfiles element in XML.");
                return null;
            }

            var adapterProfiles = new List<AdapterProfileDescriptor>();
            foreach (var profileElement in adapterProfilesElement.Elements("AdapterProfile"))
            {
                var profileAttrs = profileElement.GetAttributeDict();
                var name = profileAttrs.TryGetValue("Name");
                var targetNamespace = profileAttrs.TryGetValue("TargetNamespace");
                var targetClassName = profileAttrs.TryGetValue("TargetClassName");

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(targetNamespace) || string.IsNullOrEmpty(targetClassName))
                {
                    logger?.LogWarning($"Skipping adapter profile with missing name, targetNamespace, or targetClassName.");
                    continue;
                }

                var supportedCapabilities = new List<string>();
                foreach (var supportedCapElement in profileElement.Elements("SupportsCapability"))
                {
                    var supportedAttrs = supportedCapElement.GetAttributeDict();
                    var supportedName = supportedAttrs.TryGetValue("Name");

                    if (string.IsNullOrEmpty(supportedName))
                    {
                        logger?.LogWarning($"Skipping supported capability with missing name in adapter profile {name}.");
                        continue;
                    }

                    supportedCapabilities.Add(supportedName);
                }

                adapterProfiles.Add(new AdapterProfileDescriptor(
                    name,
                    targetNamespace,
                    targetClassName,
                    supportedCapabilities));
            }

            return new GeneratorParams(
                generatedCapabilitiesNamespace,
                generatedInterfacesNamespace,
                capabilities,
                adapterProfiles);
        }
        catch (Exception ex)
        {
            logger?.LogError($"Error parsing XML: {ex.Message}");
            return null;
        }
    }

    public static string? Generate(GeneratorParams? parameters, IBvLogger? logger)
    {
        if (!parameters.HasValue)
        {
            logger?.LogError("Cannot generate code from null parameters.");
            return null;
        }

        var generatedFiles = new List<GeneratedFile>();
        var erpCapabilitiesClass = GenerateErpCapabilitiesClass(parameters.Value);
        generatedFiles.Add(erpCapabilitiesClass);

        // Generate interface definitions for all capabilities
        var interfaceDefinitions = new List<GeneratedFile>();
        foreach (var capability in parameters.Value.Capabilities)
        {
            var interfaceDefinition = GenerateInterfaceDefinition(
                capability,
                parameters.Value.GeneratedInterfacesNamespace);
            interfaceDefinitions.Add(interfaceDefinition);
            generatedFiles.Add(interfaceDefinition);
        }

        // Generate capability defaults classes for each adapter profile
        foreach (var profile in parameters.Value.AdapterProfiles)
        {
            var capabilityDefaultsClass = GenerateCapabilityDefaultsClass(
                profile,
                parameters.Value.Capabilities,
                parameters.Value.GeneratedCapabilitiesNamespace);

            generatedFiles.Add(capabilityDefaultsClass);

            var adapterClass = GenerateAdapterClass(
                profile,
                parameters.Value.Capabilities,
                parameters.Value.GeneratedInterfacesNamespace);

            generatedFiles.Add(adapterClass);

            var diClass = GenerateDiRegistrationClass(
                profile,
                parameters.Value.Capabilities,
                parameters.Value.GeneratedInterfacesNamespace);

            generatedFiles.Add(diClass);
        }

        // Return a summary of what we generated
        return $"Generated {generatedFiles.Count} files:\r\n" +
               string.Join("\r\n", generatedFiles.Select(f => $"- {f.FileName}"));
    }

    public static GeneratedFile GenerateErpCapabilitiesClass(GeneratorParams parameters)
    {
        var className = "ErpCapabilities";
        var filePath = $"{className}.g.cs";

        var properties = string.Join("\r\n\r\n    ", parameters.Capabilities.Select(c =>
            $"/// <summary>\r\n    /// {c.Description}\r\n    /// </summary>\r\n    public bool {CapabilityNameToPropertyName(c.Name)} {{ get; set; }}"));

        var code = $$"""
// <auto-generated/>
// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace {{parameters.GeneratedCapabilitiesNamespace}};

/// <summary>
/// Represents the capabilities supported by an ERP integration adapter.
/// </summary>
public class {{className}}
{
    {{properties}}
}
""";

        return new GeneratedFile(filePath, parameters.GeneratedCapabilitiesNamespace, code);
    }

    public static GeneratedFile GenerateCapabilityDefaultsClass(
        AdapterProfileDescriptor profile,
        List<CapabilityDescriptor> allCapabilities,
        string generatedCapabilitiesNamespace)
    {
        var className = $"{profile.Name}CapabilitiesDefaults";
        var filePath = $"{profile.Name}/{className}.g.cs";

        var propertyAssignments = string.Join("\r\n            ", allCapabilities.Select(c =>
        {
            var propertyName = CapabilityNameToPropertyName(c.Name);
            var isSupported = profile.SupportedCapabilities.Contains(c.Name, StringComparer.Ordinal);
            return $"{propertyName} = {isSupported.ToString().ToLowerInvariant()},";
        }));

        var code = $$"""
// <auto-generated/>
// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace {{generatedCapabilitiesNamespace}};

/// <summary>
/// Default capabilities configuration for the {{profile.Name}} ERP adapter.
/// </summary>
public static class {{className}}
{
    /// <summary>
    /// Gets the default capabilities supported by the {{profile.Name}} ERP adapter.
    /// </summary>
    public static ErpCapabilities DefaultCapabilities { get; } = new ErpCapabilities
    {
            {{propertyAssignments}}
    };
}
""";

        return new GeneratedFile(filePath, generatedCapabilitiesNamespace, code);
    }

    public static GeneratedFile GenerateInterfaceDefinition(
        CapabilityDescriptor capability,
        string generatedInterfacesNamespace)
    {
        var interfaceName = capability.Interface;
        var filePath = $"Capabilities/Generated/{interfaceName}.g.cs";

        var code = $$"""
// <auto-generated/>
// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace {{generatedInterfacesNamespace}};

/// <summary>
/// {{capability.Description}}
/// </summary>
/// <remarks>
/// Generated from capability: {{capability.Name}}
/// </remarks>
public partial interface {{interfaceName}}
{
    // Implementation details will be defined in manual partial interface files
}
""";

        return new GeneratedFile(filePath, generatedInterfacesNamespace, code);
    }

    public static GeneratedFile GenerateAdapterClass(
        AdapterProfileDescriptor profile,
        List<CapabilityDescriptor> allCapabilities,
        string generatedInterfacesNamespace)
    {
        var filePath = $"{profile.Name}/{profile.TargetClassName}.g.cs";

        // Find all interfaces for the supported capabilities
        var supportedInterfaces = allCapabilities
            .Where(c => profile.SupportedCapabilities.Contains(c.Name, StringComparer.Ordinal))
            .Select(c => c.Interface)
            .ToList();

        // Format with one interface per line
        var interfaceList = supportedInterfaces.Count > 0
            ? "\r\n    " + string.Join(",\r\n    ", supportedInterfaces)
            : string.Empty;

        var code = $$"""
// <auto-generated/>
// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace {{profile.TargetNamespace}};

using {{generatedInterfacesNamespace}};

/// <summary>
/// Partial implementation of the {{profile.Name}} ERP adapter that defines its supported capabilities.
/// </summary>
public partial record {{profile.TargetClassName}} :{{interfaceList}}
{
    // This file is auto-generated. Interface implementations should be provided in the non-generated part of this partial class.
}
""";

        return new GeneratedFile(filePath, profile.TargetNamespace, code);
    }

    public static GeneratedFile GenerateDiRegistrationClass(
        AdapterProfileDescriptor profile,
        List<CapabilityDescriptor> allCapabilities,
        string generatedInterfacesNamespace)
    {
        var filePath = $"{profile.Name}/{profile.TargetClassName}ServiceCollectionExtensions.g.cs";

        // Find all interfaces for the supported capabilities
        var supportedInterfaces = allCapabilities
            .Where(c => profile.SupportedCapabilities.Contains(c.Name, StringComparer.Ordinal))
            .Select(c => $"services.AddKeyedScoped<{c.Interface}>(\"{profile.Name.ToLowerInvariant()}\", (svc, _) => svc.GetRequiredService<{profile.TargetClassName}>());")
            .ToList();

        // Format with one interface per line
        var interfaceList = supportedInterfaces.Count > 0
            ? string.Join("\r\n            ", supportedInterfaces)
            : string.Empty;

        var code = $$"""
// <auto-generated/>
// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace {{profile.TargetNamespace}};

using Microsoft.Extensions.DependencyInjection;
using {{generatedInterfacesNamespace}};

public static class {{profile.TargetClassName}}ServiceCollectionExtensions
{
    public static IServiceCollection Add{{profile.TargetClassName}}(this IServiceCollection services)
    {
        services.AddScoped<{{profile.TargetClassName}}>();
        {{interfaceList}}

        return services;
    }
}
""";

        return new GeneratedFile(filePath, profile.TargetNamespace, code);
    }

    public static GeneratedFile GenerateAllAdapterRegistrationClass(
        ErpCapabilityGenerator.GeneratorParams generatorParams,
        string generatedInterfacesNamespace)
    {
        var filePath = $"ErpCapabilitiesServiceCollectionExtensions.g.cs";

        // Format with one interface per line
        var interfaceList = generatorParams.AdapterProfiles.Count > 0
            ? string.Join("\r\n        ", generatorParams.AdapterProfiles.Select(p => $"services.Add{p.TargetClassName}();"))
            : string.Empty;

        var namespaceList = generatorParams.AdapterProfiles.Count > 0
            ? string.Join("\r\n", generatorParams.AdapterProfiles.Select(p => $"using {p.TargetNamespace};"))
            : string.Empty;

        var code = $$"""
// <auto-generated/>
// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

namespace {{generatorParams.GeneratedInterfacesNamespace}};

using Microsoft.Extensions.DependencyInjection;
using {{generatedInterfacesNamespace}};
{{namespaceList}}

public static class ErpCapabilitiesServiceCollectionExtensions
{
    public static IServiceCollection AddErpCapabilities(this IServiceCollection services)
    {
        services.AddScoped<IErpCapabilityResolver, ErpCapabilityResolver>();
        {{interfaceList}}

        return services;
    }
}
""";

        return new GeneratedFile(filePath, generatorParams.GeneratedInterfacesNamespace, code);
    }

    public static string CapabilityNameToPropertyName(string capabilityName)
    {
        // Convert capability names like "Job.CanRead" to property names like "CanReadJob"
        var parts = capabilityName.Split('.');
        if (parts.Length == 2)
        {
            return $"{parts[1]}{parts[0]}";
        }

        // If the format is different, just return the original name
        return capabilityName;
    }

    public readonly record struct GeneratorParams
    {
        public readonly string GeneratedCapabilitiesNamespace;
        public readonly string GeneratedInterfacesNamespace;
        public readonly List<CapabilityDescriptor> Capabilities;
        public readonly List<AdapterProfileDescriptor> AdapterProfiles;

        public GeneratorParams(
            string generatedCapabilitiesNamespace,
            string generatedInterfacesNamespace,
            List<CapabilityDescriptor> capabilities,
            List<AdapterProfileDescriptor> adapterProfiles)
        {
            this.GeneratedCapabilitiesNamespace = generatedCapabilitiesNamespace;
            this.GeneratedInterfacesNamespace = generatedInterfacesNamespace;
            this.Capabilities = capabilities;
            this.AdapterProfiles = adapterProfiles;
        }
    }

    public readonly record struct CapabilityDescriptor
    {
        public readonly string Name;
        public readonly string Interface;
        public readonly string Description;

        public CapabilityDescriptor(
            string name,
            string interfaceName,
            string description)
        {
            this.Name = name;
            this.Interface = interfaceName;
            this.Description = description;
        }
    }

    public readonly record struct AdapterProfileDescriptor
    {
        public readonly string Name;
        public readonly string TargetNamespace;
        public readonly string TargetClassName;
        public readonly List<string> SupportedCapabilities;

        public AdapterProfileDescriptor(
            string name,
            string targetNamespace,
            string targetClassName,
            List<string> supportedCapabilities)
        {
            this.Name = name;
            this.TargetNamespace = targetNamespace;
            this.TargetClassName = targetClassName;
            this.SupportedCapabilities = supportedCapabilities;
        }
    }

    public readonly record struct GeneratedFile
    {
        public readonly string FileName;
        public readonly string Namespace;
        public readonly string Content;

        public GeneratedFile(
            string fileName,
            string @namespace,
            string content)
        {
            this.FileName = fileName;
            this.Namespace = @namespace;
            this.Content = content;
        }
    }
}
