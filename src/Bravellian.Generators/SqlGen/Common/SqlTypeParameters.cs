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

namespace Bravellian.Generators.SqlGen.Common;

/// <summary>
/// Represents parameters for SQL types that require them.
/// </summary>
public class SqlTypeParameters
{
    /// <summary>
    /// Gets or sets the precision for decimal types.
    /// </summary>
    public int? Precision { get; }

    /// <summary>
    /// Gets or sets the scale for decimal types.
    /// </summary>
    public int? Scale { get; }

    /// <summary>
    /// Gets or sets the maximum length for string types.
    /// </summary>
    public int? MaxLength { get; }

    /// <summary>
    /// Gets or sets whether this is a MAX type (for strings and binary).
    /// </summary>
    public bool IsMax { get; }

    private SqlTypeParameters(int? precision = null, int? scale = null, int? maxLength = null, bool isMax = false)
    {
        Precision = precision;
        Scale = scale;
        MaxLength = maxLength;
        IsMax = isMax;
    }

    /// <summary>
    /// Creates parameters for a decimal type.
    /// </summary>
    /// <param name="precision">The precision.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The parameters.</returns>
    public static SqlTypeParameters ForDecimal(int precision, int scale)
    {
        return new SqlTypeParameters(precision: precision, scale: scale);
    }

    /// <summary>
    /// Creates parameters for a string type.
    /// </summary>
    /// <param name="maxLength">The maximum length, or null for MAX.</param>
    /// <returns>The parameters.</returns>
    public static SqlTypeParameters ForString(int? maxLength = null)
    {
        return new SqlTypeParameters(maxLength: maxLength, isMax: maxLength == null);
    }

    /// <summary>
    /// Creates parameters for a binary type.
    /// </summary>
    /// <param name="maxLength">The maximum length, or null for MAX.</param>
    /// <returns>The parameters.</returns>
    public static SqlTypeParameters ForBinary(int? maxLength = null)
    {
        return new SqlTypeParameters(maxLength: maxLength, isMax: maxLength == null);
    }

    /// <summary>
    /// Creates parameters for a fixed-length string type.
    /// </summary>
    /// <param name="length">The fixed length.</param>
    /// <returns>The parameters.</returns>
    public static SqlTypeParameters ForFixedLengthString(int length)
    {
        return new SqlTypeParameters(maxLength: length, isMax: false);
    }

    /// <summary>
    /// Creates parameters for a MAX length string type.
    /// </summary>
    /// <returns>The parameters.</returns>
    public static SqlTypeParameters ForMaxLengthString()
    {
        return new SqlTypeParameters(maxLength: null, isMax: true);
    }

    /// <summary>
    /// Gets a string representation of the parameters.
    /// </summary>
    /// <returns>The string representation.</returns>
    public override string ToString()
    {
        if (Precision.HasValue && Scale.HasValue)
        {
            return $"({Precision},{Scale})";
        }
        if (MaxLength.HasValue)
        {
            return $"({MaxLength})";
        }
        if (IsMax)
        {
            return "(MAX)";
        }
        return string.Empty;
    }
}

