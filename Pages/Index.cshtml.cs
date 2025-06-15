using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Database.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost(string username, string password, bool remember)
        {
            // Aquí iría la lógica de autenticación
            // Por ahora es solo un ejemplo
            
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Usuario y contraseña son requeridos");
                return Page();
            }

            // Ejemplo básico de validación (debes implementar tu propia lógica segura)
            if (username == "admin" && password == "admin123")
            {
                // Autenticación exitosa - redirigir a la página principal
                return RedirectToPage("/Dashboard");
            }

            ModelState.AddModelError(string.Empty, "Credenciales inválidas");
            return Page();
        }
    }
}