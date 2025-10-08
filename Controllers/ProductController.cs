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
        private readonly IFunctionsApi _api;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IFunctionsApi api, ILogger<ProductController> logger)
        {
            _api = api;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _api.GetProductsAsync();
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

                    // Delegate creation (and optional image) to Function App
                    await _api.CreateProductAsync(product, imageFile);
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
            var existing = await _api.GetProductAsync(id);
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
                    // Delegate update (and optional new image) to Function App
                    await _api.UpdateProductAsync(product.RowKey, product, imageFile);
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
                await _api.DeleteProductAsync(id);
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
