using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Database.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DashboardModel> _logger;

        public string? DbConnectionMessage { get; set; }
        public bool IsConnected { get; set; }
        public string Query { get; set; } = string.Empty; // Propiedad para la consulta SQL
        public DataTable ResultsTable { get; set; } = new(); // Propiedad para almacenar resultados

        public DashboardModel(IConfiguration configuration, ILogger<DashboardModel> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public void OnGet()
        {
            // Obtener el mensaje de conexión del HttpContext
            if (HttpContext.Items.TryGetValue("DbConnectionMessage", out var message))
            {
                DbConnectionMessage = message?.ToString();
                ViewData["DbConnectionMessage"] = DbConnectionMessage;
            }
        }

        public async Task<IActionResult> OnPostExecuteQueryAsync()
        {
            if (string.IsNullOrWhiteSpace(Query))
            {
                DbConnectionMessage = "La consulta SQL no puede estar vacía.";
                return Page();
            }

            try
            {
                using var sqlConnection = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                await sqlConnection.OpenAsync();
                using var command = new SqlCommand(Query, sqlConnection);

                if (Query.TrimStart().StartsWith("SELECT", System.StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = await command.ExecuteReaderAsync();
                    ResultsTable.Load(reader);
                    DbConnectionMessage = "Consulta SELECT ejecutada con éxito.";
                }
                else
                {
                    var affectedRows = await command.ExecuteNonQueryAsync();
                    ResultsTable.Columns.Add("Resultado");
                    ResultsTable.Rows.Add($"Filas afectadas: {affectedRows}");
                    DbConnectionMessage = "Consulta ejecutada con éxito.";
                }
            }
            catch (SqlException ex)
            {
                DbConnectionMessage = $"Error SQL: {ex.Message}";
                _logger.LogError(ex, "Error al ejecutar la consulta SQL.");
            }
            catch (Exception ex)
            {
                DbConnectionMessage = $"Error general: {ex.Message}";
                _logger.LogError(ex, "Error al ejecutar la consulta SQL.");
            }

            return Page();
        }

        public async Task<bool> TestConnectionAsync()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            _logger.LogInformation($"Intentando conectar con: {connectionString}");

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                DbConnectionMessage = "Conexión exitosa a la base de datos.";
                IsConnected = true;
                return true;
            }
            catch (SqlException ex)
            {
                DbConnectionMessage = $"Error SQL: {ex.Message}";
                IsConnected = false;
                _logger.LogError(ex, "Error al conectar con la base de datos.");
            }
            catch (Exception ex)
            {
                DbConnectionMessage = $"Error general: {ex.Message}";
                IsConnected = false;
                _logger.LogError(ex, "Error al conectar con la base de datos.");
            }

            return false;
        }
    }
}