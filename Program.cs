using ST10439055_CLDVPOE.Services;
using System.Net.Http.Headers;

namespace ST10439055_CLDVPOE
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            
            // Register Azure Storage Service
            builder.Services.AddScoped<IAzureStorageService, AzureStorageService>();

            // Register Functions API HttpClient and typed client
            var functionsBaseUrl = builder.Configuration["Functions:BaseUrl"];
            var functionsApiKey = builder.Configuration["Functions:ApiKey"];

            if (!string.IsNullOrWhiteSpace(functionsBaseUrl))
            {
                builder.Services.AddHttpClient("Functions", client =>
                {
                    client.BaseAddress = new Uri(functionsBaseUrl);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (!string.IsNullOrWhiteSpace(functionsApiKey))
                    {
                        // Many Azure Functions use x-functions-key header; keep it ready
                        client.DefaultRequestHeaders.Add("x-functions-key", functionsApiKey);
                    }
                });

                builder.Services.AddScoped<IFunctionsApi, FunctionsApiClient>();
            }

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
