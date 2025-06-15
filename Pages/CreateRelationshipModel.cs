using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;

namespace Database.Pages
{
    public class CreateRelationshipModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public CreateRelationshipModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        [Required(ErrorMessage = "Debe seleccionar la tabla principal.")]
        public string Table1 { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Debe seleccionar la tabla relacionada.")]
        public string Table2 { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Debe seleccionar el tipo de relación.")]
        public string RelationshipType { get; set; } = "OneToMany"; // Default: Uno a muchos

        [BindProperty]
        public string Column1 { get; set; } = string.Empty;

        [BindProperty]
        public string Column2 { get; set; } = string.Empty;

        public List<string> AvailableTables { get; set; } = new();
        public List<string> ColumnsTable1 { get; set; } = new();
        public List<string> ColumnsTable2 { get; set; } = new();

        public string SqlResult { get; set; } = string.Empty;

        public void OnGet()
        {
            LoadAvailableTables();
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                LoadAvailableTables();
                SqlResult = "❌ El modelo no es válido. Por favor, revisa los campos.";
                return Page();
            }

            try
            {
                string sql = GenerateRelationshipSql(Table1, Table2, RelationshipType, Column1, Column2);
                SqlResult = sql;

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                SqlResult += "\n\n✅ Relación creada con éxito.";
            }
            catch (SqlException ex)
            {
                SqlResult = $"❌ Error SQL: {ex.Message}";
            }
            catch (Exception ex)
            {
                SqlResult = $"❌ Error: {ex.Message}";
            }

            LoadAvailableTables();
            return Page();
        }

        private void LoadAvailableTables()
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
                            AvailableTables.Add(reader.GetString(0));
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(Table1) && AvailableTables.Contains(Table1))
            {
                List<string> tempColumnsTable1;
                LoadColumns(Table1, out tempColumnsTable1);
                ColumnsTable1 = tempColumnsTable1;
            }
            if (!string.IsNullOrEmpty(Table2) && AvailableTables.Contains(Table2))
            {
                List<string> tempColumnsTable2;
                LoadColumns(Table2, out tempColumnsTable2);
                ColumnsTable2 = tempColumnsTable2;
            }
        }

        private void LoadColumns(string tableName, out List<string> columns)
        {
            columns = new List<string>();
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            columns.Add(reader.GetString(0)); // Nombre de la columna
                        }
                    }
                }
            }
        }

        private string GenerateRelationshipSql(string table1, string table2, string relationshipType, string column1, string column2)
        {
            var sql = new StringBuilder();

            if (relationshipType == "OneToOne" || relationshipType == "OneToMany")
            {
                sql.AppendLine($"ALTER TABLE [{table2}] ADD CONSTRAINT FK_{table2}_{table1}");
                sql.AppendLine($"FOREIGN KEY ([{column2}]) REFERENCES [{table1}] ([{column1}])");
            }
            else if (relationshipType == "ManyToMany")
            {
                string junctionTable = $"{table1}_{table2}_Junction";
                sql.AppendLine($"CREATE TABLE [{junctionTable}] (");
                sql.AppendLine($"    [{table1}_{column1}] {GetColumnType(table1, column1)} NOT NULL,");
                sql.AppendLine($"    [{table2}_{column2}] {GetColumnType(table2, column2)} NOT NULL,");
                sql.AppendLine($"    CONSTRAINT PK_{junctionTable} PRIMARY KEY ([{table1}_{column1}], [{table2}_{column2}]),");
                sql.AppendLine($"    CONSTRAINT FK_{junctionTable}_{table1} FOREIGN KEY ([{table1}_{column1}]) REFERENCES [{table1}] ([{column1}]),");
                sql.AppendLine($"    CONSTRAINT FK_{junctionTable}_{table2} FOREIGN KEY ([{table2}_{column2}]) REFERENCES [{table2}] ([{column2}])");
                sql.AppendLine(");");
            }

            return sql.ToString();
        }

        private string GetColumnType(string tableName, string columnName)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand($"SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}'", conn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return reader.GetString(0); // Tipo de columna
                        }
                    }
                }
            }
            return "INT"; // Valor por defecto si no se encuentra
        }
    }
}