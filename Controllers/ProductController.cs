using Microsoft.AspNetCore.Mvc;
using ST10439055_CLDVPOE.Models;
using ST10439055_CLDVPOE.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace ST10439055_CLDVPOE.Controllers
{
    public class ProductController : Controller
    {
        private readonly IAzureStorageService _storage;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IAzureStorageService storage, ILogger<ProductController> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _storage.GetAllEntitiesAsync<Product>();
            return View(products);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            // Parse PriceString into decimal Price
            if (!string.IsNullOrWhiteSpace(product.PriceString))
            {
                var normalized = product.PriceString.Trim();
                if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var parsed))
                {
                    product.Price = parsed;
                }
                else if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out parsed))
                {
                    product.Price = parsed;
                }
                else
                {
                    ModelState.AddModelError("PriceString", "Enter a valid price (e.g., 29.99)");
                }
            }
            _logger.LogInformation("Final product price: {Price}", product.Price);

            if (ModelState.IsValid)
            {
                try
                {
                    if (product.Price <= 0)
                    {
                        ModelState.AddModelError("Price", "Price must be greater than $0.00");
                        return View(product);
                    }

                    // Upload image if provided
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var imageUrl = await _storage.UploadImageAsync(imageFile, "product-images");
                        product.ImageUrl = imageUrl;
                    }

                    await _storage.AddEntityAsync(product);
                    TempData["Success"] = $"Product '{product.ProductName}' created successfully with price {product.Price:C}!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating product");
                    ModelState.AddModelError("", $"Error creating product: {ex.Message}");
                }
            }
            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var existing = await _storage.GetEntityAsync<Product>("Product", id);
            if (existing == null) return NotFound();
            return View(existing);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product product, IFormFile? imageFile)
        {
            // Parse PriceString for edit
            if (!string.IsNullOrWhiteSpace(product.PriceString))
            {
                var normalized = product.PriceString.Trim();
                if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var parsed))
                {
                    product.Price = parsed;
                }
                else if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out parsed))
                {
                    product.Price = parsed;
                }
                else
                {
                    ModelState.AddModelError("PriceString", "Enter a valid price (e.g., 29.99)");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get the original product to preserve ETag
                    var originalProduct = await _storage.GetEntityAsync<Product>("Product", product.RowKey);
                    if (originalProduct == null)
                    {
                        return NotFound();
                    }

                    // Update properties but keep the original ETag
                    originalProduct.ProductName = product.ProductName;
                    originalProduct.Description = product.Description;
                    originalProduct.Price = product.Price;
                    originalProduct.StockAvailable = product.StockAvailable;

                    // Upload new image if provided
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var imageUrl = await _storage.UploadImageAsync(imageFile, "product-images");
                        originalProduct.ImageUrl = imageUrl;
                    }

                    await _storage.UpdateEntityAsync(originalProduct);
                    TempData["Success"] = "Product updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating product: {Message}", ex.Message);
                    ModelState.AddModelError("", $"Error updating product: {ex.Message}");
                }
            }
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _storage.DeleteEntityAsync<Product>("Product", id);
                TempData["Success"] = "Product deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting product: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
