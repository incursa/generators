// Copyright (c) Bravellian
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

namespace Bravellian.Generators.SqlGen.Pipeline.3_CSharpTransformation.Models
{

    /// <summary>
    /// Represents the origin of a specific piece of information about a property (e.g., its type or name).
    /// </summary>
    public class PropertySourceInfo
{
    /// <summary>
    /// Gets the aspect of the property this information pertains to (e.g., "SQL Type", "C# Type", "Property Name").
    /// </summary>
    public string Aspect { get; }

    /// <summary>
    /// Gets the source of the information (e.g., "SQL DDL", "Global Type Mapping", "Column Override").
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets a detailed description of the source (e.g., "Rule with priority 100", "CREATE TABLE statement").
    /// </summary>
    public string Details { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertySourceInfo"/> class.
    /// </summary>
    public PropertySourceInfo(string aspect, string source, string details)
    {
        this.Aspect = aspect;
        this.Source = source;
        this.Details = details;
    }
}
}
