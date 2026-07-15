using Microsoft.AspNetCore.Identity;

namespace Mango.Services.AuthAPI.Models
{
    public class ApplicationUser: IdentityUser
    {
        //TODO: More fields like timestamp is missing
        public string  Name { get; set; }
    }
}
