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

        public async Task OnGetAsync() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Query))
            {
                Message = "Por favor, escribe una consulta.";
                return Page();
            }

            await ExecuteQuery();
            return Page();
        }

        private async Task ExecuteQuery()
        {
            try
            {
                using var sqlConnection = new SqlConnection(_configuration.GetConnectionString("SqlServer"));
                await sqlConnection.OpenAsync();
                using var command = new SqlCommand(Query, sqlConnection);

                if (Query.TrimStart().StartsWith("SELECT", System.StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = await command.ExecuteReaderAsync();
                    ResultsTable.Load(reader);
                    Message = "Consulta SELECT ejecutada con éxito.";
                }
                else
                {
                    var affected = await command.ExecuteNonQueryAsync();
                    ResultsTable.Columns.Add("Resultado");
                    ResultsTable.Rows.Add($"Filas afectadas: {affected}");
                    Message = "Consulta ejecutada con éxito.";
                }
            }
            catch (SqlException ex)
            {
                Message = $"Error: {ex.Message}";
            }
        }
    }
}