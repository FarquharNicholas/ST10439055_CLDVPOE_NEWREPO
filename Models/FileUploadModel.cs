using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ST10439055_CLDVPOE.Models
{
    public class FileUploadModel
    {
        [Required]
        [Display(Name = "Proof of Payment")]
        public IFormFile? ProofOfPayment { get; set; }

        [Display(Name = "Order ID")]
        public string? OrderId { get; set; }

        [Display(Name = "Customer Name")]
        public string? CustomerName { get; set; }
    }
}
