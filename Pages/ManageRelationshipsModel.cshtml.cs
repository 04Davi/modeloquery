using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Database.Pages
{
    public class ManageRelationshipsModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ManageRelationshipsModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public List<TableInfo> Tables { get; set; } = new();

        public string SqlResult { get; set; } = string.Empty;

        public void OnGet()
        {
            LoadTableInfo();
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                LoadTableInfo();
                SqlResult = "❌ El modelo no es válido. Por favor, revisa los campos.";
                return Page();
            }

            try
            {
                string sql = GenerateRelationshipSql();
                if (string.IsNullOrEmpty(sql))
                {
                    SqlResult = "❌ No se generó ningún SQL para ejecutar.";
                    return Page();
                }

                SqlResult = sql; // Mostrar el SQL generado para depuración

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery(); // Ejecutar el SQL
                    }
                }

                SqlResult += "\n\n✅ Relaciones gestionadas con éxito.";
                LoadTableInfo(); // Recargar información actualizada
            }
            catch (SqlException ex)
            {
                SqlResult = $"❌ Error SQL: {ex.Message}";
            }
            catch (Exception ex)
            {
                SqlResult = $"❌ Error: {ex.Message}";
            }

            return Page();
        }

        private void LoadTableInfo()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE';", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string tableName = reader.GetString(0);
                            Tables.Add(new TableInfo
                            {
                                Name = tableName,
                                Columns = LoadColumns(tableName),
                                PrimaryKey = GetPrimaryKey(tableName),
                                ForeignKeys = GetForeignKeys(tableName)
                            });
                        }
                    }
                }
            }

            // Actualizar las propiedades IsPrimaryKey e IsForeignKey basadas en los datos cargados
            foreach (var table in Tables)
            {
                foreach (var col in table.Columns)
                {
                    col.IsPrimaryKey = (table.PrimaryKey == col.Name);
                    col.IsForeignKey = table.ForeignKeys.Contains(col.Name);
                    // Inicializar ReferenceTable y ReferenceColumn si es una clave foránea
                    if (col.IsForeignKey && string.IsNullOrEmpty(col.ReferenceTable))
                    {
                        col.ReferenceTable = Tables.FirstOrDefault(t => t.Name != table.Name)?.Name; // Valor por defecto
                        col.ReferenceColumn = Tables.FirstOrDefault(t => t.Name == col.ReferenceTable)?.PrimaryKey; // Valor por defecto
                    }
                }
            }
        }

        private List<ColumnInfo> LoadColumns(string tableName)
        {
            var columns = new List<ColumnInfo>();
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand($"SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            columns.Add(new ColumnInfo
                            {
                                Name = reader.GetString(0),
                                DataType = reader.GetString(1),
                                IsPrimaryKey = false,
                                IsForeignKey = false
                            });
                        }
                    }
                }
            }
            return columns;
        }

        private string GetPrimaryKey(string tableName)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
                    "WHERE TABLE_NAME = @tableName AND CONSTRAINT_NAME LIKE 'PK%'", conn))
                {
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetString(0);
                        }
                    }
                }
                return null;
            }
        }

        private List<string> GetForeignKeys(string tableName)
        {
            var foreignKeys = new List<string>();
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
                    "WHERE TABLE_NAME = @tableName AND CONSTRAINT_NAME LIKE 'FK%'", conn))
                {
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            foreignKeys.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return foreignKeys;
        }

        private string GenerateRelationshipSql()
        {
            var sql = new StringBuilder();

            foreach (var table in Tables)
            {
                // Actualizar clave primaria
                string currentPk = GetPrimaryKey(table.Name);
                if (!string.IsNullOrEmpty(table.PrimaryKey) && table.PrimaryKey != currentPk)
                {
                    if (!string.IsNullOrEmpty(currentPk))
                    {
                        sql.AppendLine($"ALTER TABLE [{table.Name}] DROP CONSTRAINT PK_{table.Name};");
                    }
                    sql.AppendLine($"ALTER TABLE [{table.Name}] ADD CONSTRAINT PK_{table.Name} PRIMARY KEY ([{table.PrimaryKey}]);");
                }

                // Gestionar claves foráneas
                var existingFks = GetForeignKeys(table.Name);
                foreach (var col in table.Columns)
                {
                    if (col.IsForeignKey && !existingFks.Contains(col.Name))
                    {
                        if (!string.IsNullOrEmpty(col.ReferenceTable) && !string.IsNullOrEmpty(col.ReferenceColumn))
                        {
                            sql.AppendLine($"ALTER TABLE [{table.Name}] ADD CONSTRAINT FK_{table.Name}_{col.ReferenceTable} " +
                                           $"FOREIGN KEY ([{col.Name}]) REFERENCES [{col.ReferenceTable}] ([{col.ReferenceColumn}]);");
                        }
                    }
                    else if (!col.IsForeignKey && existingFks.Contains(col.Name))
                    {
                        // Obtener el nombre exacto de la restricción foránea
                        string constraintName = GetForeignKeyConstraintName(table.Name, col.Name);
                        if (!string.IsNullOrEmpty(constraintName))
                        {
                            sql.AppendLine($"ALTER TABLE [{table.Name}] DROP CONSTRAINT [{constraintName}];");
                        }
                    }
                }
            }

            return sql.Length > 0 ? sql.ToString() : string.Empty;
        }

        private string GetForeignKeyConstraintName(string tableName, string columnName)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(
                    "SELECT CONSTRAINT_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE " +
                    "WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName AND CONSTRAINT_NAME LIKE 'FK%'", conn))
                {
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    cmd.Parameters.AddWithValue("@columnName", columnName);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetString(0);
                        }
                    }
                }
            }
            return null;
        }
    }

    public class TableInfo
    {
        public string Name { get; set; }
        public List<ColumnInfo> Columns { get; set; } = new();
        public string PrimaryKey { get; set; }
        public List<string> ForeignKeys { get; set; } = new();
    }

    public class ColumnInfo
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsForeignKey { get; set; }
        public string ReferenceTable { get; set; }
        public string ReferenceColumn { get; set; }
    }
}