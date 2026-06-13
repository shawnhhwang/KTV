using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Dapper;

namespace KTV
{
    public static class DatabaseBootstrap
    {
        public static void Initialize(string connectionString)
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            string dbFilePath = builder.DataSource;

            if (!string.IsNullOrEmpty(dbFilePath) && dbFilePath != ":memory:")
            {
                string? directory = Path.GetDirectoryName(Path.GetFullPath(dbFilePath));
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS Songs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT NOT NULL,
                        Artist TEXT NOT NULL,
                        FilePath TEXT NOT NULL,
                        AccompanimentPath TEXT,
                        VocalPath TEXT,
                        Source TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );";
                
                connection.Execute(createTableQuery);
            }
        }
    }
}
