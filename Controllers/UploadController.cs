using Microsoft.AspNetCore.Mvc;
using ST10439055_CLDVPOE.Models;
using ST10439055_CLDVPOE.Services;

namespace ST10439055_CLDVPOE.Controllers
{
    public class UploadController : Controller
    {
        private readonly IAzureStorageService _storage;

        public UploadController(IAzureStorageService storage)
        {
            _storage = storage;
        }

        public IActionResult Index()
        {
            return View(new FileUploadModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(FileUploadModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (model.ProofOfPayment != null && model.ProofOfPayment.Length > 0)
                    {
                        // Upload to blob storage
                        var fileName = await _storage.UploadFileAsync(model.ProofOfPayment, "payment-proofs");

                        // Also upload to file share for contracts
                        await _storage.UploadToFileShareAsync(model.ProofOfPayment, "contracts", "payments");

                        TempData["Success"] = $"File uploaded successfully! File name: {fileName}";

                        // Clear the model for a fresh form
                        return View(new FileUploadModel());
                    }
                    else
                    {
                        ModelState.AddModelError("ProofOfPayment", "Please select a file to upload.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error uploading file: {ex.Message}");
                }
            }
            return View(model);
        }
    }
}
