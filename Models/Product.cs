using Azure;
using Azure.Data.Tables;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.Serialization;

namespace ST10439055_CLDVPOE.Models
{
    public class Product : ITableEntity
    {
        public string PartitionKey { get; set; } = "Product";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        [Display(Name = "Product ID")]
        public string ProductId => RowKey;

        [Required]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required")]
        [Display(Name = "Price")]
        public string PriceString { get; set; } = string.Empty;

        [Display(Name = "Price")]
        [IgnoreDataMember]
        public decimal Price
        {
            get
            {
                if (decimal.TryParse(PriceString, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var invariantResult))
                {
                    return invariantResult;
                }
                if (decimal.TryParse(PriceString, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out var currentResult))
                {
                    return currentResult;
                }
                return 0m;
            }
            set
            {
                PriceString = value.ToString("F2", CultureInfo.InvariantCulture);
            }
        }

        [Required]
        [Display(Name = "Stock Available")]
        public int StockAvailable { get; set; }

        [Display(Name = "Image URL")]
        public string ImageUrl { get; set; } = string.Empty;
    }
}
