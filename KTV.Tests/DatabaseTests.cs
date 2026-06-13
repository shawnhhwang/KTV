using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Dapper;
using Xunit;

namespace KTV.Tests
{
    public class DatabaseTests
    {
        [Fact]
        public void Initialize_CreatesDatabaseAndSongsTable()
        {
            // Arrange
            string tempDbFile = Path.Combine(Path.GetTempPath(), $"KtvTest_{Guid.NewGuid():N}.sqlite");
            string connectionString = $"Data Source={tempDbFile};";

            try
            {
                // Act
                DatabaseBootstrap.Initialize(connectionString);

                // Assert
                Assert.True(File.Exists(tempDbFile), "Database file should be created");

                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    
                    // Verify Songs table exists
                    var tableSchema = connection.Query(@"
                        SELECT name FROM sqlite_master 
                        WHERE type='table' AND name='Songs';
                    ").ToList();
                    
                    Assert.Single(tableSchema);
                    
                    // Verify columns by inserting a dummy song and querying it
                    connection.Execute(@"
                        INSERT INTO Songs (Title, Artist, FilePath, Source)
                        VALUES ('Test Title', 'Test Artist', 'TestPath.mp4', 'Local');
                    ");

                    var songs = connection.Query("SELECT * FROM Songs;").ToList();
                    Assert.Single(songs);
                    var song = songs[0];
                    
                    Assert.Equal("Test Title", (string)song.Title);
                    Assert.Equal("Test Artist", (string)song.Artist);
                    Assert.Equal("TestPath.mp4", (string)song.FilePath);
                    Assert.Equal("Local", (string)song.Source);
                }
            }
            finally
            {
                // Clean up
                if (File.Exists(tempDbFile))
                {
                    try
                    {
                        // Sqlite might hold file lock unless connection pool is cleared
                        SqliteConnection.ClearAllPools();
                        File.Delete(tempDbFile);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
    }
}
