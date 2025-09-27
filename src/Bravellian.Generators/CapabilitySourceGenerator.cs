// Copyright (c) Samuel McAravey
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Bravellian.Generators;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

public class CapabilitySourceGenerator
{
    protected Regex FileExtensionRegex { get; } = new Regex(@"(?:.*\.capabilities\.xml|.*\.capabilities\.json|.*\.capabilities-only\.json|.*\.adapter-profile\.json|.*\.erp-capabilities\.xml|.*\.erp-capabilities\.json|.*\.erp-capabilities-only\.json|.*\.erp-adapter-profile\.json|_generate\.xml)$");

    /// <summary>
    /// Derives a standardized interface name from a capability name.
    /// Example: "Vendor.CanRead" becomes "IVendorCanRead"
    /// </summary>
    private static string DeriveInterfaceName(string capabilityName)
    {
        // Remove dots and other special characters, then prefix with 'I'
        var cleanName = capabilityName.Replace(".", "").Replace("-", "").Replace("_", "");
        return $"I{cleanName}";
    }

    protected IEnumerable<(string fileName, string source)>? Generate(string filePath, string fileContent, CancellationToken cancellationToken)
    {
        var fileExtension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        if (fileExtension == ".json")
        {
            return GenerateFromJson(fileContent);
        }
        else
        {
            return GenerateFromXml(fileContent);
        }
    }

    /// <summary>
    /// Public wrapper for CLI usage
    /// </summary>
    public IEnumerable<(string fileName, string source)>? GenerateFromFiles(string filePath, string fileContent, CancellationToken cancellationToken = default)
    {
        return Generate(filePath, fileContent, cancellationToken);
    }

    private IEnumerable<(string fileName, string source)>? GenerateFromXml(string fileContent)
    {
        var xdoc = XDocument.Parse(fileContent);
        if (xdoc.Root == null) return null;

        // Look for ErpCapabilityDefinitions elements
        var elements = xdoc.Root.Elements()
            .Where(e => e.Name.LocalName == "ErpCapabilityDefinitions")
            .ToList();

        // If no direct ErpCapabilityDefinitions, check if the root is one
        if (!elements.Any() && string.Equals(xdoc.Root.Name.LocalName, "ErpCapabilityDefinitions", System.StringComparison.OrdinalIgnoreCase))
        {
            elements.Add(xdoc.Root);
        }

        if (!elements.Any()) return null;

        List<(string fileName, string source)> generated = new();

        foreach (var element in elements)
        {
            var genParams = ErpCapabilityGenerator.GetParams(element, null);
            if (genParams == null) continue;

            // Generate all the individual files that the ERP capability generator creates
            var generatedFiles = GenerateAllFiles(genParams.Value);
            generated.AddRange(generatedFiles);
        }

        return generated;
    }

    private IEnumerable<(string fileName, string source)>? GenerateFromJson(string fileContent)
    {
        try
        {
            using var document = JsonDocument.Parse(fileContent);
            var root = document.RootElement;

            // Check if this is a capabilities-only file or adapter profile file
            if (root.TryGetProperty("capabilities", out _) && root.TryGetProperty("adapterProfiles", out _))
            {
                // Combined format (original)
                var genParams = ParseGeneratorParamsFromJson(root);
                if (genParams == null) return null;

                var generated = new List<(string fileName, string source)>();
                var generatedFiles = GenerateAllFiles(genParams.Value);
                generated.AddRange(generatedFiles);
                return generated;
            }
            else if (root.TryGetProperty("capabilities", out _))
            {
                // Capabilities-only format
                var genParams = ParseCapabilitiesOnlyFromJson(root);
                if (genParams == null) return null;

                var generated = new List<(string fileName, string source)>();
                var generatedFiles = GenerateCapabilitiesOnlyFiles(genParams.Value);
                generated.AddRange(generatedFiles);
                return generated;
            }
            else if (root.TryGetProperty("adapterProfile", out _))
            {
                // Adapter profile format
                var genParams = ParseAdapterProfileFromJson(root);
                if (genParams == null) return null;

                var generated = new List<(string fileName, string source)>();
                var generatedFiles = GenerateAdapterProfileFiles(genParams.Value);
                generated.AddRange(generatedFiles);
                return generated;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static List<(string fileName, string source)> GenerateAllFiles(ErpCapabilityGenerator.GeneratorParams parameters)
    {
        var generated = new List<(string fileName, string source)>();

        // Generate the main ErpCapabilities class
        var erpCapabilitiesClass = ErpCapabilityGenerator.GenerateErpCapabilitiesClass(parameters);
        generated.Add((erpCapabilitiesClass.FileName, erpCapabilitiesClass.Content));

        // Generate interface definitions for all capabilities
        foreach (var capability in parameters.Capabilities)
        {
            var interfaceDefinition = ErpCapabilityGenerator.GenerateInterfaceDefinition(
                capability,
                parameters.GeneratedInterfacesNamespace);
            generated.Add((interfaceDefinition.FileName, interfaceDefinition.Content));
        }

        // Generate capability defaults classes and adapter classes for each adapter profile
        foreach (var profile in parameters.AdapterProfiles)
        {
            var capabilityDefaults = ErpCapabilityGenerator.GenerateCapabilityDefaultsClass(
                profile,
                parameters.Capabilities,
                parameters.GeneratedCapabilitiesNamespace);
            generated.Add((capabilityDefaults.FileName, capabilityDefaults.Content));

            var adapterClass = ErpCapabilityGenerator.GenerateAdapterClass(
                profile,
                parameters.Capabilities,
                parameters.GeneratedInterfacesNamespace);
            generated.Add((adapterClass.FileName, adapterClass.Content));

            var diRegistrationClass = ErpCapabilityGenerator.GenerateDiRegistrationClass(
                profile,
                parameters.Capabilities,
                parameters.GeneratedInterfacesNamespace);
            generated.Add((diRegistrationClass.FileName, diRegistrationClass.Content));
        }

        // Generate the main DI registration class if there are adapter profiles
        if (parameters.AdapterProfiles.Any())
        {
            var allAdapterRegistrationClass = ErpCapabilityGenerator.GenerateAllAdapterRegistrationClass(
                parameters,
                parameters.GeneratedInterfacesNamespace);
            generated.Add((allAdapterRegistrationClass.FileName, allAdapterRegistrationClass.Content));
        }

        return generated;
    }

    private static List<(string fileName, string source)> GenerateCapabilitiesOnlyFiles(ErpCapabilityGenerator.GeneratorParams parameters)
    {
        var generated = new List<(string fileName, string source)>();

        // Generate the main ErpCapabilities class
        var erpCapabilitiesClass = ErpCapabilityGenerator.GenerateErpCapabilitiesClass(parameters);
        generated.Add((erpCapabilitiesClass.FileName, erpCapabilitiesClass.Content));

        // Generate interface definitions for all capabilities
        foreach (var capability in parameters.Capabilities)
        {
            var interfaceDefinition = ErpCapabilityGenerator.GenerateInterfaceDefinition(
                capability,
                parameters.GeneratedInterfacesNamespace);
            generated.Add((interfaceDefinition.FileName, interfaceDefinition.Content));
        }

        return generated;
    }

    private static List<(string fileName, string source)> GenerateAdapterProfileFiles(ErpCapabilityGenerator.GeneratorParams parameters)
    {
        var generated = new List<(string fileName, string source)>();

        // Should only have one adapter profile for this format
        if (parameters.AdapterProfiles.Any())
        {
            var profile = parameters.AdapterProfiles.First();

            var capabilityDefaults = ErpCapabilityGenerator.GenerateCapabilityDefaultsClass(
                profile,
                parameters.Capabilities,
                parameters.GeneratedCapabilitiesNamespace);
            generated.Add((capabilityDefaults.FileName, capabilityDefaults.Content));

            var adapterClass = ErpCapabilityGenerator.GenerateAdapterClass(
                profile,
                parameters.Capabilities,
                parameters.GeneratedInterfacesNamespace);
            generated.Add((adapterClass.FileName, adapterClass.Content));

            var diRegistrationClass = ErpCapabilityGenerator.GenerateDiRegistrationClass(
                profile,
                parameters.Capabilities,
                parameters.GeneratedInterfacesNamespace);
            generated.Add((diRegistrationClass.FileName, diRegistrationClass.Content));
        }

        return generated;
    }

    private static ErpCapabilityGenerator.GeneratorParams? ParseCapabilitiesOnlyFromJson(JsonElement root)
    {
        // Required fields
        if (!root.TryGetProperty("generatedCapabilitiesNamespace", out var capabilitiesNsElement) ||
            !root.TryGetProperty("generatedInterfacesNamespace", out var interfacesNsElement) ||
            !root.TryGetProperty("capabilities", out var capabilitiesElement))
        {
            return null;
        }

        var generatedCapabilitiesNamespace = capabilitiesNsElement.GetString();
        var generatedInterfacesNamespace = interfacesNsElement.GetString();

        if (string.IsNullOrEmpty(generatedCapabilitiesNamespace) || string.IsNullOrEmpty(generatedInterfacesNamespace))
        {
            return null;
        }

        // Parse capabilities
        var capabilities = ParseCapabilitiesArray(capabilitiesElement);

        return new ErpCapabilityGenerator.GeneratorParams(
            generatedCapabilitiesNamespace,
            generatedInterfacesNamespace,
            capabilities,
            new List<ErpCapabilityGenerator.AdapterProfileDescriptor>());
    }

    private static ErpCapabilityGenerator.GeneratorParams? ParseAdapterProfileFromJson(JsonElement root)
    {
        // Required fields
        if (!root.TryGetProperty("capabilitiesReference", out var capabilitiesRefElement) ||
            !root.TryGetProperty("adapterProfile", out var adapterProfileElement))
        {
            return null;
        }

        // Parse capabilities reference
        if (!capabilitiesRefElement.TryGetProperty("generatedCapabilitiesNamespace", out var capabilitiesNsElement) ||
            !capabilitiesRefElement.TryGetProperty("generatedInterfacesNamespace", out var interfacesNsElement))
        {
            return null;
        }

        var generatedCapabilitiesNamespace = capabilitiesNsElement.GetString();
        var generatedInterfacesNamespace = interfacesNsElement.GetString();

        if (string.IsNullOrEmpty(generatedCapabilitiesNamespace) || string.IsNullOrEmpty(generatedInterfacesNamespace))
        {
            return null;
        }

        // Parse adapter profile
        if (!adapterProfileElement.TryGetProperty("name", out var profileNameElement) ||
            !adapterProfileElement.TryGetProperty("targetNamespace", out var targetNsElement) ||
            !adapterProfileElement.TryGetProperty("targetClassName", out var targetClassElement))
        {
            return null;
        }

        var profileName = profileNameElement.GetString();
        var targetNamespace = targetNsElement.GetString();
        var targetClassName = targetClassElement.GetString();

        if (string.IsNullOrEmpty(profileName) || string.IsNullOrEmpty(targetNamespace) || string.IsNullOrEmpty(targetClassName))
        {
            return null;
        }

        var supportedCapabilities = new List<string>();
        if (adapterProfileElement.TryGetProperty("supportedCapabilities", out var supportedElement) && supportedElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var supportedCapElement in supportedElement.EnumerateArray())
            {
                var supportedCapName = supportedCapElement.GetString();
                if (!string.IsNullOrEmpty(supportedCapName))
                {
                    supportedCapabilities.Add(supportedCapName);
                }
            }
        }

        // For adapter profile files, we create capability descriptors based on the references using derived interface names
        var capabilities = supportedCapabilities.Select(name => 
            new ErpCapabilityGenerator.CapabilityDescriptor(name, DeriveInterfaceName(name), $"Capability for {name}")).ToList();

        var adapterProfile = new ErpCapabilityGenerator.AdapterProfileDescriptor(
            profileName,
            targetNamespace,
            targetClassName,
            supportedCapabilities);

        return new ErpCapabilityGenerator.GeneratorParams(
            generatedCapabilitiesNamespace,
            generatedInterfacesNamespace,
            capabilities,
            new List<ErpCapabilityGenerator.AdapterProfileDescriptor> { adapterProfile });
    }

    private static List<ErpCapabilityGenerator.CapabilityDescriptor> ParseCapabilitiesArray(JsonElement capabilitiesElement)
    {
        var capabilities = new List<ErpCapabilityGenerator.CapabilityDescriptor>();
        
        if (capabilitiesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var capabilityElement in capabilitiesElement.EnumerateArray())
            {
                if (!capabilityElement.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var name = nameElement.GetString();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                // Derive interface name from capability name
                var interfaceName = DeriveInterfaceName(name);
                
                var description = capabilityElement.TryGetProperty("description", out var descElement) 
                    ? descElement.GetString() ?? string.Empty
                    : string.Empty;

                capabilities.Add(new ErpCapabilityGenerator.CapabilityDescriptor(name, interfaceName, description));
            }
        }

        return capabilities;
    }

    private static ErpCapabilityGenerator.GeneratorParams? ParseGeneratorParamsFromJson(JsonElement root)
    {
        // Required fields
        if (!root.TryGetProperty("generatedCapabilitiesNamespace", out var capabilitiesNsElement) ||
            !root.TryGetProperty("generatedInterfacesNamespace", out var interfacesNsElement) ||
            !root.TryGetProperty("capabilities", out var capabilitiesElement))
        {
            return null;
        }

        var generatedCapabilitiesNamespace = capabilitiesNsElement.GetString();
        var generatedInterfacesNamespace = interfacesNsElement.GetString();

        if (string.IsNullOrEmpty(generatedCapabilitiesNamespace) || string.IsNullOrEmpty(generatedInterfacesNamespace))
        {
            return null;
        }

        // Parse capabilities
        var capabilities = ParseCapabilitiesArray(capabilitiesElement);

        // Parse adapter profiles (optional)
        var adapterProfiles = new List<ErpCapabilityGenerator.AdapterProfileDescriptor>();
        if (root.TryGetProperty("adapterProfiles", out var profilesElement) && profilesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var profileElement in profilesElement.EnumerateArray())
            {
                if (!profileElement.TryGetProperty("name", out var profileNameElement) ||
                    !profileElement.TryGetProperty("targetNamespace", out var targetNsElement) ||
                    !profileElement.TryGetProperty("targetClassName", out var targetClassElement))
                {
                    continue;
                }

                var profileName = profileNameElement.GetString();
                var targetNamespace = targetNsElement.GetString();
                var targetClassName = targetClassElement.GetString();

                if (string.IsNullOrEmpty(profileName) || string.IsNullOrEmpty(targetNamespace) || string.IsNullOrEmpty(targetClassName))
                {
                    continue;
                }

                var supportedCapabilities = new List<string>();
                if (profileElement.TryGetProperty("supportedCapabilities", out var supportedElement) && supportedElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var supportedCapElement in supportedElement.EnumerateArray())
                    {
                        var supportedCapName = supportedCapElement.GetString();
                        if (!string.IsNullOrEmpty(supportedCapName))
                        {
                            supportedCapabilities.Add(supportedCapName);
                        }
                    }
                }

                adapterProfiles.Add(new ErpCapabilityGenerator.AdapterProfileDescriptor(
                    profileName,
                    targetNamespace,
                    targetClassName,
                    supportedCapabilities));
            }
        }

        return new ErpCapabilityGenerator.GeneratorParams(
            generatedCapabilitiesNamespace,
            generatedInterfacesNamespace,
            capabilities,
            adapterProfiles);
    }
}
