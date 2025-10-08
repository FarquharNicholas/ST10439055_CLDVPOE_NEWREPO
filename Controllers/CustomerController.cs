using Microsoft.AspNetCore.Mvc;
using ST10439055_CLDVPOE.Models;
using ST10439055_CLDVPOE.Services;

namespace ST10439055_CLDVPOE.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IAzureStorageService _storage;

        public CustomerController(IAzureStorageService storage)
        {
            _storage = storage;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _storage.GetAllEntitiesAsync<Customer>();
            return View(customers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Customer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _storage.AddEntityAsync(customer);
                    TempData["Success"] = "Customer created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error creating customer: {ex.Message}");
                }
            }
            return View(customer);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var existing = await _storage.GetEntityAsync<Customer>("Customer", id);
            if (existing == null) return NotFound();
            return View(existing);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer customer)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Retrieve the original to preserve ETag and concurrency
                    var original = await _storage.GetEntityAsync<Customer>("Customer", customer.RowKey);
                    if (original == null)
                    {
                        return NotFound();
                    }

                    // Copy editable fields
                    original.Name = customer.Name;
                    original.Surname = customer.Surname;
                    original.Username = customer.Username;
                    original.Email = customer.Email;
                    original.ShippingAddress = customer.ShippingAddress;

                    await _storage.UpdateEntityAsync(original);
                    TempData["Success"] = "Customer updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error updating customer: {ex.Message}");
                }
            }
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _storage.DeleteEntityAsync<Customer>("Customer", id);
                TempData["Success"] = "Customer deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting customer: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
