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
/// Represents the core SQL Server data types.
/// </summary>
public enum SqlCoreType
{
    /// <summary>
    /// Unknown or unsupported type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Integer types.
    /// </summary>
    Int,
    BigInt,
    SmallInt,
    TinyInt,

    /// <summary>
    /// Decimal types.
    /// </summary>
    Decimal,
    Numeric,
    Money,
    SmallMoney,

    /// <summary>
    /// Floating point types.
    /// </summary>
    Float,
    Real,

    /// <summary>
    /// String types.
    /// </summary>
    Char,
    NChar,
    VarChar,
    NVarChar,
    Text,
    NText,

    /// <summary>
    /// Binary types.
    /// </summary>
    Binary,
    VarBinary,
    Image,

    /// <summary>
    /// Date and time types.
    /// </summary>
    Date,
    Time,
    DateTime,
    DateTime2,
    DateTimeOffset,
    SmallDateTime,

    /// <summary>
    /// Other types.
    /// </summary>
    Bit,
    UniqueIdentifier,
    Xml,
    HierarchyId,
    Geography,
    Geometry,
}

