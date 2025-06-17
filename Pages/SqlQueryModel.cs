using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Database.Pages
{
    public class SqlQueryModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public SqlQueryModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public string Query { get; set; }

        public DataTable ResultsTable { get; set; } = new();
        public string Message { get; set; }
        public bool IsDangerousCommand { get; set; }

        public async Task OnGetAsync()
        {
            IsDangerousCommand = false;
        }

        public async Task<IActionResult> OnPostAsync(string confirm = "")
        {
            if (string.IsNullOrWhiteSpace(Query))
            {
                Message = "Por favor, escribe una consulta.";
                return Page();
            }

            // Detecta si es comando peligroso
            IsDangerousCommand = IsCommandDangerous(Query);

            // Si es consulta normal (SELECT), ejecuta directamente
            if (!IsDangerousCommand)
            {
                await ExecuteQuery();
                return Page();
            }

            // Si es comando peligroso y no se ha confirmado, muestra mensaje
            if (string.IsNullOrEmpty(confirm))
            {
                Message = "Advertencia: Este comando puede modificar o eliminar datos. ¿Estás seguro de ejecutarlo?";
                return Page();
            }

            // Si se confirmó, ejecuta
            if (confirm == "true")
            {
                await ExecuteQuery();
            }

            return Page();
        }

        private async Task ExecuteQuery()
        {
            try
            {
                using var sqlConnection = new SqlConnection(_configuration.GetConnectionString("SqlServer"));
                await sqlConnection.OpenAsync();

                string[] batches = Query.Split(new[] { "GO" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var batch in batches)
                {
                    string sqlCommand = batch.Trim();
                    if (string.IsNullOrEmpty(sqlCommand)) continue;

                    using var command = new SqlCommand(sqlCommand, sqlConnection);
                    ResultsTable.Clear();
                    ResultsTable.Columns.Clear();

                    if (sqlCommand.StartsWith("SELECT", System.StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = await command.ExecuteReaderAsync();
                        ResultsTable.Load(reader);
                        Message = "Consulta SELECT ejecutada con éxito.";
                    }
                    else
                    {
                        int affected = await command.ExecuteNonQueryAsync();

                        ResultsTable.Columns.Add("Resultado");
                        ResultsTable.Rows.Add($"Filas afectadas: {affected}");
                        Message = "Comando ejecutado con éxito.";
                    }
                }
            }
            catch (SqlException ex)
            {
                Message = $"Error: {ex.Message}";
            }
        }

        private bool IsCommandDangerous(string query)
        {
            string[] dangerousKeywords = { "DELETE", "DROP", "TRUNCATE", "UPDATE", "INSERT" };
            string firstWord = query.TrimStart().Split(' ')[0].ToUpper();

            foreach (var keyword in dangerousKeywords)
            {
                if (firstWord.StartsWith(keyword))
                {
                    return true;
                }
            }

            return false;
        }
    }
}