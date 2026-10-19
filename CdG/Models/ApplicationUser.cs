using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CdG.Models
{
    public class ApplicationUser : IdentityUser
    {
        [PersonalData]
        [StringLength(100)]
        public string? Nome { get; set; }
    }
}