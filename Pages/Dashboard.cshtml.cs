using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Database.Pages
{
    public class DashboardModel : PageModel
    {
        public string? DbConnectionMessage { get; set; }

        public void OnGet()
        {
            // Obtener el mensaje de conexión del HttpContext
            if (HttpContext.Items.TryGetValue("DbConnectionMessage", out var message))
            {
                DbConnectionMessage = message?.ToString();
                ViewData["DbConnectionMessage"] = DbConnectionMessage;
            }
        }
    }
}