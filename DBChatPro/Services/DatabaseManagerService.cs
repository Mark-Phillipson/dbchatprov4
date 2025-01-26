using DBChatPro.Models;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace DBChatPro
{
    public class DatabaseManagerService(MySqlDatabaseService mySqlDb, SqlServerDatabaseService msSqlDb, PostgresDatabaseService postgresDb) : IDatabaseService
    {
        public async Task<List<List<string>>> GetDataTable(AIConnection conn, string sqlQuery)
        {
            switch (conn.DatabaseType)
            {
                case "MSSQL":
                    return await msSqlDb.GetDataTable(conn, sqlQuery);
                case "MYSQL":
                    return await mySqlDb.GetDataTable(conn, sqlQuery);
                case "POSTGRES":
                    return await postgresDb.GetDataTable(conn, sqlQuery);
            }

            return null;
        }

        public async Task<DatabaseSchema> GenerateSchema(AIConnection conn)
        {
            switch (conn.DatabaseType)
            {
                case "MSSQL":
                    return await msSqlDb.GenerateSchema(conn);
                case "MYSQL":
                    return await mySqlDb.GenerateSchema(conn);
                case "POSTGRESQL":
                    return await postgresDb.GenerateSchema(conn);
            }

            return new() { SchemaStructured = new List<TableSchema>(), SchemaRaw = new List<string>() };
        }
    }
}
