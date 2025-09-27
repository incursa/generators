// CONFIDENTIAL - Copyright (c) Bravellian LLC. All rights reserved.
// See NOTICE.md for full restrictions and usage terms.

#nullable enable

using System;
using Bravellian.Generators.SqlGen.Pipeline._1_Ingestion.Model;

namespace Bravellian.Generators.SqlGen.Common;

/// <summary>
/// Extension methods for PwSqlType.
/// </summary>
public static class SqlTypeExtensions
{
    /// <summary>
    /// Creates a PwSqlType from a SQL type string.
    /// </summary>
    /// <param name="sqlType">The SQL type string to parse.</param>
    /// <returns>The corresponding PwSqlType, or Unknown if not recognized.</returns>
    public static PwSqlType FromSqlString(this string sqlType)
    {
        if (string.IsNullOrWhiteSpace(sqlType))
        {
            return PwSqlType.Unknown;
        }
        
        try
        {
            // Use the existing Parse method
            return PwSqlType.Parse(sqlType);
        }
        catch
        {
            // If parsing fails, return Unknown
            return PwSqlType.Unknown;
        }
    }
    
    /// <summary>
    /// Safe method to parse SQL type that won't throw exceptions.
    /// </summary>
    /// <param name="sqlType">The SQL type to parse</param>
    /// <param name="logger">Optional logger for errors</param>
    /// <returns>The parsed SQL type or Unknown if parsing fails</returns>
    public static PwSqlType SafeParseSqlType(this string sqlType, IBvLogger? logger = null)
    {
        try
        {
            return PwSqlType.Parse(sqlType);
        }
        catch (Exception ex)
        {
            logger?.LogError($"Failed to parse SQL type '{sqlType}': {ex.Message}");
            return PwSqlType.Unknown;
        }
    }
}

