using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.RegularExpressions;

namespace Database.Pages
{
    public class CreateTableModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public CreateTableModel(IConfiguration configuration)
        {
            _configuration = configuration;
            SqlDataTypes = GetCommonSqlDataTypes();
        }

        public List<SqlDataType> SqlDataTypes { get; set; } = new();

        [BindProperty]
        [Required(ErrorMessage = "El nombre de la tabla es obligatorio.")]
        public string TableName { get; set; } = string.Empty;

        [BindProperty]
        public List<TableColumn> Columns { get; set; } = new();

        public string SqlResult { get; set; } = string.Empty;

        public void OnGet()
        {
            if (Columns.Count == 0)
            {
                Columns.Add(new TableColumn());
            }
        }

        public async Task<IActionResult> OnPostAddColumn()
        {
            if (!await TryUpdateModelAsync(Columns, "Columns"))
            {
                ModelState.AddModelError("", "Error al actualizar los datos del formulario.");
                return Page();
            }

            Columns.Add(new TableColumn());
            return Page();
        }

        public async Task<IActionResult> OnPostRemoveColumn(int index)
        {
            if (!await TryUpdateModelAsync(Columns, "Columns"))
            {
                ModelState.AddModelError("", "Error al actualizar los datos del formulario.");
                return Page();
            }

            if (index >= 0 && index < Columns.Count)
            {
                Columns.RemoveAt(index);
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                SqlResult = "Por favor, completa todos los campos obligatorios.";
                return Page();
            }

            try
            {
                var sql = GenerarSqlCrearTabla(); // Usa el método que te mostré antes

                using var connection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                using var command = new SqlCommand(sql, connection);
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();

                SqlResult = "Tabla creada correctamente.";
            }
            catch (SqlException ex)
            {
                SqlResult = $"Error SQL: {ex.Message}";
            }

            return Page();
        }

        private string GenerateCreateTableSql(string tableName, List<TableColumn> columns)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("El nombre de la tabla no puede estar vacío.");

            if (columns == null || columns.Count == 0)
                throw new ArgumentException("Debe definir al menos una columna.");

            var validTypes = SqlDataTypes.Select(t => t.Name.ToUpper()).ToHashSet();
            var sql = new StringBuilder();
            sql.AppendLine($"CREATE TABLE [{tableName}] (");

            List<string> pkColumns = new();

            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (string.IsNullOrWhiteSpace(col.Name))
                    throw new ArgumentException($"El nombre de la columna en la posición {i + 1} no puede estar vacío.");

                string sqlType = col.DataType.ToUpper();
                if (!validTypes.Contains(sqlType))
                    throw new ArgumentException($"Tipo de dato no soportado: {sqlType} en columna {col.Name}.");

                var typeDef = SqlDataTypes.First(t => t.Name.ToUpper() == sqlType);
                string lengthStr = string.Empty;

                if (typeDef.RequiresPrecisionScale) // DECIMAL
                {
                    if (string.IsNullOrWhiteSpace(col.Length))
                    {
                        col.Length = "10,2"; // Valor por defecto si no se especifica
                        ModelState.AddModelError("", $"Se ha establecido un valor por defecto '10,2' para la precisión/escala de {sqlType} en columna {col.Name}.");
                    }
                    if (!Regex.IsMatch(col.Length, @"^\d+,\d+$"))
                        throw new ArgumentException($"Formato de longitud inválido para {sqlType} en columna {col.Name}. Use 'precision,scale' (e.g., '10,2').");

                    var parts = col.Length.Split(',');
                    if (!int.TryParse(parts[0], out int precision) || !int.TryParse(parts[1], out int scale) || precision <= 0 || scale < 0 || scale > precision)
                        throw new ArgumentException($"Valores inválidos para precisión/escala en {sqlType} en columna {col.Name}.");
                    lengthStr = $"({col.Length})";
                }
                else if (typeDef.RequiresLength) // VARCHAR, NVARCHAR, CHAR, NCHAR
                {
                    if (string.IsNullOrWhiteSpace(col.Length))
                    {
                        col.Length = sqlType is "VARCHAR" or "NVARCHAR" ? "255" : "1"; // Valor por defecto
                        ModelState.AddModelError("", $"Se ha establecido un valor por defecto '{col.Length}' para la longitud de {sqlType} en columna {col.Name}.");
                    }
                    if (col.Length.ToUpper() == "MAX" && (sqlType == "VARCHAR" || sqlType == "NVARCHAR"))
                        lengthStr = "(MAX)";
                    else if (int.TryParse(col.Length, out int len) && len > 0)
                        lengthStr = $"({len})";
                    else
                        throw new ArgumentException($"Longitud inválida para {sqlType} en columna {col.Name}.");
                }
                else // INT, FLOAT, BIT, DATE (no requiere longitud)
                {
                    if (!string.IsNullOrWhiteSpace(col.Length))
                    {
                        ModelState.AddModelError("", $"No se debe especificar longitud para el tipo {sqlType} en columna {col.Name}. Se ignorará.");
                    }
                    lengthStr = ""; // No agregar longitud
                }

                string nullStr = col.AllowNull ? "NULL" : "NOT NULL";
                sql.Append($"  [{col.Name}] {sqlType}{lengthStr} {nullStr}");

                if (col.IsPrimaryKey)
                {
                    pkColumns.Add(col.Name);
                }

                bool isLastColumn = (i == columns.Count - 1);
                if (!isLastColumn || pkColumns.Count > 0)
                    sql.AppendLine(",");
                else
                    sql.AppendLine();
            }

            if (pkColumns.Count > 0)
            {
                sql.AppendLine($"  CONSTRAINT [PK_{tableName}] PRIMARY KEY ({string.Join(", ", pkColumns.Select(c => $"[{c}]"))})");
            }

            sql.AppendLine(");");

            return sql.ToString();
        }

        private List<SqlDataType> GetCommonSqlDataTypes()
        {
            return new List<SqlDataType>
            {
                new SqlDataType { Name = "INT", RequiresLength = false },
                new SqlDataType { Name = "VARCHAR", RequiresLength = true },
                new SqlDataType { Name = "NVARCHAR", RequiresLength = true },
                new SqlDataType { Name = "CHAR", RequiresLength = true },
                new SqlDataType { Name = "NCHAR", RequiresLength = true },
                new SqlDataType { Name = "DATE", RequiresLength = false },
                new SqlDataType { Name = "FLOAT", RequiresLength = false },
                new SqlDataType { Name = "BIT", RequiresLength = false },
                new SqlDataType { Name = "DECIMAL", RequiresPrecisionScale = true }
            };
        }

        public string GenerarSqlCrearTabla()
        {
            var primaryKeys = Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {TableName} (");

            for (int i = 0; i < Columns.Count; i++)
            {
                var col = Columns[i];
                sb.Append($"    {col.Name} {col.DataType}");

                if (!string.IsNullOrEmpty(col.Length))
                    sb.Append($"({col.Length})");

                if (!col.AllowNull)
                    sb.Append(" NOT NULL");

                if (i < Columns.Count - 1 || primaryKeys.Any())
                    sb.Append(",");

                sb.AppendLine();
            }

            if (primaryKeys.Any())
            {
                sb.AppendLine($"    PRIMARY KEY ({string.Join(", ", primaryKeys)})");
            }

            sb.AppendLine(");");
            return sb.ToString();
        }
    }

    public class TableColumn
    {
        [Required(ErrorMessage = "El nombre de la columna es obligatorio.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de datos es obligatorio.")]
        public string DataType { get; set; } = "VARCHAR";

        // Hacer Length opcional para evitar fallos de validación
        public string? Length { get; set; } // Cambiado a nullable

        public bool AllowNull { get; set; } = true;

        public bool IsPrimaryKey { get; set; } = false;
    }

    public class SqlDataType
    {
        public string Name { get; set; } = string.Empty;
        public bool RequiresLength { get; set; } = false;
        public bool RequiresPrecisionScale { get; set; } = false;
    }
}