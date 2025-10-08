using Microsoft.AspNetCore.Mvc;
using ST10439055_CLDVPOE.Models;
using ST10439055_CLDVPOE.Models.ViewModels;
using ST10439055_CLDVPOE.Services;
using System.Text.Json;

namespace ST10439055_CLDVPOE.Controllers
{
    public class OrderController : Controller
    {
        private readonly IAzureStorageService _storage; // still used for queues if needed
        private readonly IFunctionsApi _api;

        public OrderController(IAzureStorageService storage, IFunctionsApi api)
        {
            _storage = storage;
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _api.GetOrdersAsync();
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var customers = await _api.GetCustomersAsync();
            var products = await _api.GetProductsAsync();
            var viewModel = new OrderCreateViewModel
            {
                Customers = customers,
                Products = products
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Get customer and product details
                    var customer = await _api.GetCustomerAsync(model.CustomerId);
                    var product = await _api.GetProductAsync(model.ProductId);

                    if (customer == null || product == null)
                    {
                        ModelState.AddModelError("", "Invalid customer or product selected.");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    // Check stock availability
                    if (product.StockAvailable < model.Quantity)
                    {
                        ModelState.AddModelError("Quantity", $"Insufficient stock. Available: {product.StockAvailable}");
                        await PopulateDropdowns(model);
                        return View(model);
                    }

                    // Create order
                    var created = await _api.CreateOrderAsync(model.CustomerId, model.ProductId, model.Quantity);

                    TempData["Success"] = "Order created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error creating order: {ex.Message}");
                }
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Order id is required");
            var order = await _api.GetOrderAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Order id is required");
            var order = await _api.GetOrderAsync(id);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order order)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Update status via Functions endpoint
                    await _api.UpdateOrderStatusAsync(order.RowKey, order.Status);
                    TempData["Success"] = "Order updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating order: {ex.Message}");
                }
            }
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    TempData["Error"] = "Invalid order id";
                }
                else
                {
                    await _api.DeleteOrderAsync(id);
                    TempData["Success"] = "Order deleted successfully!";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting order: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<JsonResult> GetProductPrice(string productId)
        {
            try
            {
                var product = await _api.GetProductAsync(productId);
                if (product != null)
                {
                    return Json(new { success = true, price = product.Price, stock = product.StockAvailable, productName = product.ProductName });
                }
                return Json(new { success = false });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateOrderStatusRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.NewStatus))
                {
                    return Json(new { success = false, message = "Invalid request" });
                }

                var order = await _api.GetOrderAsync(request.Id);
                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }

                await _api.UpdateOrderStatusAsync(order.OrderId, request.NewStatus);

                return Json(new { success = true, message = $"Order status updated to {request.NewStatus}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task PopulateDropdowns(OrderCreateViewModel model)
        {
            model.Customers = await _api.GetCustomersAsync();
            model.Products = await _api.GetProductsAsync();
        }
    }
}
