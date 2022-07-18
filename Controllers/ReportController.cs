﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReportBuilder.Web.Core.Models;

namespace ReportBuilder.Web.Core.Controllers
{
    public class ReportController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Report(int reportId, string reportName, string reportDescription, bool includeSubTotal, bool showUniqueRecords,
            bool aggregateReport, bool showDataWithGraph, string reportSql, string connectKey, string reportFilter, string reportType, int selectedFolder, string reportSeries)
        {
            var model = new DotNetReportModel
            {
                ReportId = reportId,
                ReportType = reportType,
                ReportName = HttpUtility.UrlDecode(reportName),
                ReportDescription = HttpUtility.UrlDecode(reportDescription),
                ReportSql = reportSql,
                ConnectKey = connectKey,
                IncludeSubTotals = includeSubTotal,
                ShowUniqueRecords = showUniqueRecords,
                ShowDataWithGraph = showDataWithGraph,
                SelectedFolder = selectedFolder,
                ReportSeries = !string.IsNullOrEmpty(reportSeries) ? reportSeries.Replace("%20", " ") : string.Empty,
                ReportFilter = reportFilter // json data to setup filter correctly again
            };

            return View(model);
        }

        public async Task<ActionResult> ReportLink(int reportId, int? filterId = null, string filterValue = "", bool adminMode = false)
        {
            var model = new DotNetReportModel();
            var reportApi = new ReportApiController();
            var settings = reportApi.GetSettings();

            using (var client = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("account", settings.AccountApiToken),
                    new KeyValuePair<string, string>("dataConnect", settings.DataConnectApiToken),
                    new KeyValuePair<string, string>("clientId", settings.ClientId),
                    new KeyValuePair<string, string>("userId", settings.UserId),
                    new KeyValuePair<string, string>("userRole", String.Join(",", settings.CurrentUserRole)),
                    new KeyValuePair<string, string>("reportId", reportId.ToString()),
                    new KeyValuePair<string, string>("filterId", filterId.HasValue ? filterId.ToString() : ""),
                    new KeyValuePair<string, string>("filterValue", filterValue.ToString()),
                    new KeyValuePair<string, string>("adminMode", adminMode.ToString()),
                    new KeyValuePair<string, string>("dataFilters", JsonConvert.SerializeObject(settings.DataFilters))
                });

                var response = await client.PostAsync(new Uri(settings.ApiUrl + $"/ReportApi/RunLinkedReport"), content);
                var stringContent = await response.Content.ReadAsStringAsync();

                model = JsonConvert.DeserializeObject<DotNetReportModel>(stringContent);
            }

            return View("Report", model);
        }

        public IActionResult ReportPrint(int reportId, string reportName, string reportDescription, string reportSql, string connectKey, string reportFilter, string reportType,
            int selectedFolder = 0, bool includeSubTotal = false, bool showUniqueRecords = false, bool aggregateReport = false, bool showDataWithGraph = true,
            string userId = null, string clientId = null, string currentUserRole = null, string dataFilters = "",
            string reportSeries = "", bool expandAll = false)
        {
            TempData["reportPrint"] = "true";
            TempData["userId"] = userId;
            TempData["clientId"] = clientId;
            TempData["currentUserRole"] = currentUserRole;
            TempData["dataFilters"] = dataFilters;

            var model = new DotNetReportModel
            {
                ReportId = reportId,
                ReportType = reportType,
                ReportName = HttpUtility.UrlDecode(reportName),
                ReportDescription = HttpUtility.UrlDecode(reportDescription),
                ReportSql = reportSql,
                ConnectKey = connectKey,
                IncludeSubTotals = includeSubTotal,
                ShowUniqueRecords = showUniqueRecords,
                ShowDataWithGraph = showDataWithGraph,
                SelectedFolder = selectedFolder,
                ReportSeries = !string.IsNullOrEmpty(reportSeries) ? reportSeries.Replace("%20", " ") : string.Empty,
                ReportFilter = reportFilter, // json data to setup filter correctly again
                ExpandAll = expandAll
            };

            return View(model);
        }

        public async Task<IActionResult> Dashboard(int? id = null, bool adminMode = false)
        {
            var reportApi = new ReportApiController();
            var model = new List<DotNetDasboardReportModel>();
            var settings = reportApi.GetSettings();

            var dashboards = (JArray)(await reportApi.GetDashboardsData(adminMode));
            if (!id.HasValue && dashboards.Count > 0)
            {
                id = ((dynamic)dashboards.First()).Id;
            }

            using (var client = new HttpClient())
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("account", settings.AccountApiToken),
                    new KeyValuePair<string, string>("dataConnect", settings.DataConnectApiToken),
                    new KeyValuePair<string, string>("clientId", settings.ClientId),
                    new KeyValuePair<string, string>("userId", settings.UserId),
                    new KeyValuePair<string, string>("userRole", String.Join(",", settings.CurrentUserRole)),
                    new KeyValuePair<string, string>("id", id.HasValue ? id.Value.ToString() : "0"),
                    new KeyValuePair<string, string>("adminMode", adminMode.ToString()),
                });

                var response = await client.PostAsync(new Uri(settings.ApiUrl + $"/ReportApi/LoadSavedDashboard"), content);
                var stringContent = await response.Content.ReadAsStringAsync();

                model = JsonConvert.DeserializeObject<List<DotNetDasboardReportModel>>(stringContent);
            }

            return View(new DotNetDashboardModel
            {
                Dashboards = dashboards.Select(x => (dynamic)x).ToList(),
                Reports = model
            });
        }

        [HttpPost]
        public IActionResult DownloadExcel(string reportSql, string connectKey, string reportName, bool allExpanded, string expandSqls, string columnDetails = null)
        {
            reportSql = HttpUtility.HtmlDecode(reportSql); 
            var columns = columnDetails == null ? new List<ReportHeaderColumn>() : JsonConvert.DeserializeObject<List<ReportHeaderColumn>>(columnDetails);


            var excel = DotNetReportHelper.GetExcelFile(reportSql, connectKey, reportName, allExpanded, expandSqls.Split(',').ToList(), columns);
            Response.Headers.Add("content-disposition", "attachment; filename=" + reportName + ".xlsx");
            Response.ContentType = "application/vnd.ms-excel";

            return File(excel, "application/vnd.ms-excel", reportName + ".xlsx");
        }

        [HttpPost]
        public async Task<IActionResult> DownloadPdf(string reportSql, string connectKey, string reportName, string chartData = null, string columnDetails = null)
        {
            reportSql = HttpUtility.HtmlDecode(reportSql);
            var columns = columnDetails == null ? new List<ReportHeaderColumn>() : JsonConvert.DeserializeObject<List<ReportHeaderColumn>>(columnDetails);

            var reportApi = new ReportApiController();
            var settings = reportApi.GetSettings();
            var dataFilters = settings.DataFilters != null ? JsonConvert.SerializeObject(settings.DataFilters) : "";
            var pdf = await DotNetReportHelper.GetPdfFile(reportSql, connectKey, reportName, chartData, columns);
            return File(pdf, "application/pdf", reportName + ".pdf");
        }
        [HttpPost]
        public IActionResult DownloadXml(string reportSql, string connectKey, string reportName)
        {
            reportSql = HttpUtility.HtmlDecode(reportSql);
            var xml = DotNetReportHelper.GetXmlFile(reportSql, connectKey, reportName);
            Response.Headers.Add("content-disposition", "attachment; filename=" + reportName + ".xml");
            Response.ContentType = "application/xml";

            return File(xml, "application/xml", reportName + ".xml");
        }

    }
}