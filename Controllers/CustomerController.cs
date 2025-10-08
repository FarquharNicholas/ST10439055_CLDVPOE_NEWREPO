using Microsoft.AspNetCore.Mvc;
using ST10439055_CLDVPOE.Models;
using ST10439055_CLDVPOE.Services;

namespace ST10439055_CLDVPOE.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IFunctionsApi _api;

        public CustomerController(IFunctionsApi api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _api.GetCustomersAsync();
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
                    await _api.CreateCustomerAsync(customer);
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
            var existing = await _api.GetCustomerAsync(id);
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
                    await _api.UpdateCustomerAsync(customer.RowKey, customer);
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
                await _api.DeleteCustomerAsync(id);
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
