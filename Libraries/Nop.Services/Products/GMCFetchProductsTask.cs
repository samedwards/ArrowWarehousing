using Microsoft.AspNetCore.Http;
using Nop.Core.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Services.Tasks;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Nop.Services.Products
{
    public partial class GMCFetchProductsTask : IScheduleTask
    {
        #region Fields
        private readonly IUrlRecordService _urlRecordService;
        private readonly IProductService _productService;
        private readonly ILogger _logger;
        private readonly INopFileProvider _fileProvider;
        private readonly IPictureService _pictureService;


        #endregion

        #region Ctor
        
        public GMCFetchProductsTask(IProductService productService, ILogger logger, INopFileProvider fileProvider, IUrlRecordService urlRecordService, IPictureService pictureService )

        {
            _logger = logger;
            _productService = productService;
            _fileProvider = fileProvider;
            _urlRecordService = urlRecordService;
            _pictureService = pictureService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Executes a task
        /// </summary>
        public virtual void Execute()
        {
            try
            {
                var products = _productService.SearchProducts();
                _logger.InsertLog(Core.Domain.Logging.LogLevel.Information, "Fetch products start");
                //_logger.ClearLog();
                var filePath = _fileProvider.Combine(_fileProvider.MapPath("~/wwwroot/files/"), "GMCFetch.txt");
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    string domain = "https://arrowwarehousing.co.nz/";
                   
                    Core.Domain.Media.Picture picture;
                    string pictureUrl;
                    string seName;
                    string availability;
                    string price;
                    StringBuilder sb = new StringBuilder();
                    string row = "ID" + "\t" + "title" + "\t" + "description" + "\t" + "link" + "\t" + "image_link" + "\t" + "availability" + "\t" + "price" + "\t" + "google_​​product_category" + "\t" + "identifier_exists";
                    sb.AppendLine(row);
                    for (int i = 0; i < products.Count; i++)
                    {
                        availability = products[i].StockQuantity > 0 ? "in stock" : "out of stock";
                        seName = string.Format("{0}{1}",domain, _urlRecordService.GetSeName(products[i]));
                        picture = _pictureService.GetPicturesByProductId(products[i].Id).FirstOrDefault();
                        pictureUrl = _pictureService.GetPictureUrl(picture.Id);
                        price = string.Format("{0:0.00} NZD", products[i].Price);
                        row = string.Format("{0}" + "\t" + "{1}" + "\t" + "{2}" + "\t" + "{3}" + "\t" + "{4}" + "\t" + "{5}" + "\t" + "{6}" + "\t" + "{7}" + "\t" + "false", 
                            products[i].Id, products[i].Name, products[i].ShortDescription, seName, pictureUrl,
                            availability, price, products[i].ProductCategories.FirstOrDefault().Category.Name);
                        sb.AppendLine(row);
                    }
                    byte[] bdata = Encoding.Default.GetBytes(sb.ToString());
                    fileStream.Write(bdata, 0, bdata.Length);
                    fileStream.Close();
                }
                _logger.InsertLog(Core.Domain.Logging.LogLevel.Information, "Fetch products end");
            }
            catch (Exception ex)
            {
                _logger.Error("Fetch products exception: ", ex);
            }
        }

        #endregion
    }
}
