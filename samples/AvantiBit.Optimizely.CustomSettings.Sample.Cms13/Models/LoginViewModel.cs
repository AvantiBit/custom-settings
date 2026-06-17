using System.ComponentModel.DataAnnotations;

namespace AvantiBit.Optimizely.CustomSettings.Sample.Cms13.Models;

public class LoginViewModel
{
    [Required]
    public string Username { get; set; }

    [Required]
    public string Password { get; set; }
}
