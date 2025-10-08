using Microsoft.AspNetCore.Mvc;
using ST10439055_CLDVPOE.Models;
using ST10439055_CLDVPOE.Models.ViewModels;
using ST10439055_CLDVPOE.Services;
using System.Text.Json;

namespace ST10439055_CLDVPOE.Controllers
{
    public class OrderController : Controller
    {
        private readonly IAzureStorageService _storage;

        public OrderController(IAzureStorageService storage)
        {
            _storage = storage;
        }

        public async Task<IActionResult> Index()
        {
            var orders = await _storage.GetAllEntitiesAsync<Order>();
            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var customers = await _storage.GetAllEntitiesAsync<Customer>();
            var products = await _storage.GetAllEntitiesAsync<Product>();
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
                    var customer = await _storage.GetEntityAsync<Customer>("Customer", model.CustomerId);
                    var product = await _storage.GetEntityAsync<Product>("Product", model.ProductId);

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
                    var order = new Order
                    {
                        CustomerId = model.CustomerId,
                        Username = customer.Username,
                        ProductId = model.ProductId,
                        ProductName = product.ProductName,
                        OrderDate = DateTime.SpecifyKind(model.OrderDate, DateTimeKind.Utc),
                        Quantity = model.Quantity,
                        UnitPrice = product.Price,
                        TotalPrice = product.Price * model.Quantity,
                        Status = "Submitted" // Always starts as Submitted
                    };

                    await _storage.AddEntityAsync(order);

                    // Update product stock
                    product.StockAvailable -= model.Quantity;
                    await _storage.UpdateEntityAsync(product);

                    // Send queue message for new order
                    var orderMessage = new
                    {
                        OrderId = order.OrderId,
                        CustomerId = order.CustomerId,
                        CustomerName = customer.Name + " " + customer.Surname,
                        ProductName = product.ProductName,
                        Quantity = order.Quantity,
                        TotalPrice = order.TotalPrice,
                        OrderDate = order.OrderDate,
                        Status = order.Status
                    };
                    await _storage.SendMessageAsync("order-notifications", JsonSerializer.Serialize(orderMessage));

                    // Send stock update message
                    var stockMessage = new
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        PreviousStock = product.StockAvailable + model.Quantity,
                        NewStock = product.StockAvailable,
                        UpdateBy = "Order System",
                        UpdateDate = DateTime.UtcNow
                    };
                    await _storage.SendMessageAsync("stock-updates", JsonSerializer.Serialize(stockMessage));

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
            var order = await _storage.GetEntityAsync<Order>("Order", id);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Order id is required");
            var order = await _storage.GetEntityAsync<Order>("Order", id);
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
                    // Retrieve original to preserve ETag and concurrency
                    var original = await _storage.GetEntityAsync<Order>("Order", order.RowKey);
                    if (original == null)
                    {
                        return NotFound();
                    }

                    // Update allowed fields
                    original.OrderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc);
                    original.Status = order.Status;

                    await _storage.UpdateEntityAsync(original);
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
                    await _storage.DeleteEntityAsync<Order>("Order", id);
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
                var product = await _storage.GetEntityAsync<Product>("Product", productId);
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

                var order = await _storage.GetEntityAsync<Order>("Order", request.Id);
                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }

                var previousStatus = order.Status;
                order.Status = request.NewStatus;
                await _storage.UpdateEntityAsync(order);

                // Send queue message for status update
                var statusMessage = new
                {
                    OrderId = order.OrderId,
                    CustomerId = order.CustomerId,
                    CustomerName = order.Username,
                    ProductName = order.ProductName,
                    PreviousStatus = previousStatus,
                    NewStatus = request.NewStatus,
                    UpdatedDate = DateTime.UtcNow,
                    UpdatedBy = "System"
                };
                await _storage.SendMessageAsync("order-notifications", JsonSerializer.Serialize(statusMessage));

                return Json(new { success = true, message = $"Order status updated to {request.NewStatus}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task PopulateDropdowns(OrderCreateViewModel model)
        {
            model.Customers = await _storage.GetAllEntitiesAsync<Customer>();
            model.Products = await _storage.GetAllEntitiesAsync<Product>();
        }
    }
}
