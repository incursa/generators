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

using System.Text.Json;

namespace Bravellian.Generators.Tests.SqlGenerator;

public class FileReadingTests
{
    [Fact]
    public void ReadSqlFile_WithValidFile_ShouldReadContent()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var expectedContent = """
            CREATE TABLE [dbo].[Users] (
                [Id] int IDENTITY(1,1) NOT NULL,
                [Name] nvarchar(255) NOT NULL,
                [Email] nvarchar(255) NULL,
                CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
            );
            """;

        try
        {
            File.WriteAllText(tempFile, expectedContent);

            // Act
            var actualContent = File.ReadAllText(tempFile);

            // Assert
            Assert.Equal(expectedContent, actualContent);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadSqlFile_WithNonExistentFile_ShouldThrow()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), "non-existent-file.sql");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => File.ReadAllText(nonExistentFile));
    }

    [Fact]
    public void ReadSqlFile_WithEmptyFile_ShouldReturnEmpty()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();

        try
        {
            File.WriteAllText(tempFile, "");

            // Act
            var content = File.ReadAllText(tempFile);

            // Assert
            Assert.Equal("", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadSqlFile_WithLargeFile_ShouldReadCompletely()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var largeContent = string.Join(Environment.NewLine, Enumerable.Range(1, 1000).Select(i => $"-- Comment line {i}"));

        try
        {
            File.WriteAllText(tempFile, largeContent);

            // Act
            var content = File.ReadAllText(tempFile);

            // Assert
            Assert.Equal(largeContent, content);
            Assert.Contains("-- Comment line 1", content);
            Assert.Contains("-- Comment line 1000", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadSqlFile_WithDifferentEncodings_ShouldReadCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var contentWithUnicode = "CREATE TABLE [Users] ([Name] nvarchar(255) NOT NULL); -- Unicode: éñ中文";

        try
        {
            // Write with UTF-8 encoding
            File.WriteAllText(tempFile, contentWithUnicode, System.Text.Encoding.UTF8);

            // Act
            var content = File.ReadAllText(tempFile, System.Text.Encoding.UTF8);

            // Assert
            Assert.Equal(contentWithUnicode, content);
            Assert.Contains("éñ中文", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadJsonFile_WithBasicJson_ShouldParse()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var json = """{"namespace": "Test.Data", "generateDbContext": true}""";

        try
        {
            File.WriteAllText(tempFile, json);

            // Act
            var content = File.ReadAllText(tempFile);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            // Assert
            Assert.NotNull(data);
            Assert.True(data.ContainsKey("namespace"));
            Assert.True(data.ContainsKey("generateDbContext"));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadJsonFile_WithInvalidJson_ShouldThrow()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var invalidJson = """{"namespace": "Test", "invalid": }""";

        try
        {
            File.WriteAllText(tempFile, invalidJson);

            // Act & Assert
            var content = File.ReadAllText(tempFile);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Dictionary<string, object>>(content));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadMultipleFiles_WithGlobPattern_ShouldReadAllFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var file1 = Path.Combine(tempDir, "schema1.sql");
        var file2 = Path.Combine(tempDir, "schema2.sql");
        var file3 = Path.Combine(tempDir, "config.json");

        var sql1 = "CREATE TABLE [Table1] ([Id] int);";
        var sql2 = "CREATE TABLE [Table2] ([Id] int);";
        var jsonConfig = """{"namespace": "Test"}""";

        try
        {
            File.WriteAllText(file1, sql1);
            File.WriteAllText(file2, sql2);
            File.WriteAllText(file3, jsonConfig);

            // Act
            var sqlFiles = Directory.GetFiles(tempDir, "*.sql");
            var jsonFiles = Directory.GetFiles(tempDir, "*.json");

            // Assert
            Assert.Equal(2, sqlFiles.Length);
            Assert.Single(jsonFiles);
            
            var allSqlContent = string.Join(Environment.NewLine, sqlFiles.Select(File.ReadAllText));
            Assert.Contains("CREATE TABLE [Table1]", allSqlContent);
            Assert.Contains("CREATE TABLE [Table2]", allSqlContent);
            
            var configContent = File.ReadAllText(jsonFiles.First());
            var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configContent);
            Assert.Equal("Test", config!["namespace"].ToString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReadFile_WithDifferentLineEndings_ShouldHandleCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var contentWithDifferentLineEndings = "CREATE TABLE [Test] (\r\n[Id] int NOT NULL,\n[Name] nvarchar(50)\r\n);";

        try
        {
            File.WriteAllText(tempFile, contentWithDifferentLineEndings);

            // Act
            var content = File.ReadAllText(tempFile);

            // Assert
            Assert.Equal(contentWithDifferentLineEndings, content);
            Assert.Contains("\r\n", content);
            Assert.Contains("\n", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReadFile_WithBOM_ShouldHandleCorrectly()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var content = "CREATE TABLE [Test] ([Id] int);";

        try
        {
            // Write with BOM
            var utf8WithBom = new System.Text.UTF8Encoding(true);
            File.WriteAllText(tempFile, content, utf8WithBom);

            // Act
            var readContent = File.ReadAllText(tempFile);

            // Assert
            Assert.Equal(content, readContent);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}

