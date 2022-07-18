﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ReportBuilder.Web.Core.Models;
using Newtonsoft.Json;
using System.Data;
using MySql.Data.MySqlClient;

namespace ReportBuilder.Web.Core.Controllers
{
    public class SetupController : Controller
    {
        public async Task<IActionResult> Index(string databaseApiKey = "")
        {
            var connect = GetConnection(databaseApiKey);
            var tables = new List<TableViewModel>();

            tables.AddRange(await GetTables("TABLE", connect.AccountApiKey, connect.DatabaseApiKey));
            tables.AddRange(await GetTables("VIEW", connect.AccountApiKey, connect.DatabaseApiKey));

            var model = new ManageViewModel
            {
                ApiUrl = connect.ApiUrl,
                AccountApiKey = connect.AccountApiKey,
                DatabaseApiKey = connect.DatabaseApiKey,
                Tables = tables
            };

            return View(model);
        }

        #region "Private Methods"

        private ConnectViewModel GetConnection(string databaseApiKey)
        {
            return new ConnectViewModel
            {
                ApiUrl = Startup.StaticConfig.GetValue<string>("dotNetReport:apiUrl"),
                AccountApiKey = Startup.StaticConfig.GetValue<string>("dotNetReport:accountApiToken"),
                DatabaseApiKey = string.IsNullOrEmpty(databaseApiKey) ? Startup.StaticConfig.GetValue<string>("dotNetReport:dataconnectApiToken") : databaseApiKey
            };
        }

        private async Task<string> GetConnectionString(ConnectViewModel connect)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(String.Format("{0}/ReportApi/GetDataConnectKey?account={1}&dataConnect={2}", connect.ApiUrl, connect.AccountApiKey, connect.DatabaseApiKey));

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                return DotNetReportHelper.GetConnectionString(content.Replace("\"", ""));
            }

        }

        private FieldTypes ConvertMySqlDataTypeToFieldType(string dbDataType)
        {
            if (dbDataType.Contains("int"))
                return FieldTypes.Int;

            if (dbDataType.Contains("date"))
            {
                return FieldTypes.DateTime;
            }

            if (dbDataType.Contains("text") || dbDataType.Contains("char"))
            {
                return FieldTypes.Varchar;
            }

            if (dbDataType.Contains("bit") || dbDataType.Contains("bool"))
            {
                return FieldTypes.Boolean;
            }

            if (dbDataType.Contains("currency") || dbDataType.Contains("money"))
            {
                return FieldTypes.Money;
            }

            if (dbDataType.Contains("double") || dbDataType.Contains("float"))
            {
                return FieldTypes.Double;
            }

            return FieldTypes.Varchar; // default
        }

        private async Task<List<TableViewModel>> GetApiTables(string accountKey, string dataConnectKey)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(String.Format("{0}/ReportApi/GetTables?account={1}&dataConnect={2}&clientId=", Startup.StaticConfig.GetValue<string>("dotNetReport:apiUrl"), accountKey, dataConnectKey));

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                dynamic values = JsonConvert.DeserializeObject<dynamic>(content);
                var tables = new List<TableViewModel>();
                foreach (var item in values)
                {
                    tables.Add(new TableViewModel
                    {
                        Id = item.tableId,
                        SchemaName = item.schemaName,
                        AccountIdField = item.accountIdField,
                        TableName = item.tableDbName,
                        DisplayName = item.tableName,
                        AllowedRoles = item.tableRoles.ToObject<List<string>>()
                    });

                }

                return tables;
            }
        }

        private async Task<List<ColumnViewModel>> GetApiFields(string accountKey, string dataConnectKey, int tableId)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(String.Format("{0}/ReportApi/GetFields?account={1}&dataConnect={2}&clientId={3}&tableId={4}&includeDoNotDisplay=true", Startup.StaticConfig.GetValue<string>("dotNetReport:apiUrl"), accountKey, dataConnectKey, "", tableId));

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                dynamic values = JsonConvert.DeserializeObject<dynamic>(content);

                var columns = new List<ColumnViewModel>();
                foreach (var item in values)
                {
                    var column = new ColumnViewModel
                    {
                        Id = item.fieldId,
                        ColumnName = item.fieldDbName,
                        DisplayName = item.fieldName,
                        FieldType = item.fieldType,
                        PrimaryKey = item.isPrimary,
                        ForeignKey = item.hasForeignKey,
                        DisplayOrder = item.fieldOrder,
                        ForeignKeyField = item.foreignKey,
                        ForeignValueField = item.foreignValue,
                        ForeignJoin = item.foreignJoin,
                        ForeignTable = item.foreignTable,
                        DoNotDisplay = item.doNotDisplay,
                        ForceFilter = item.forceFilter,
                        AllowedRoles = item.columnRoles.ToObject<List<string>>()
                    };

                    columns.Add(column);
                }

                return columns;
            }
        }

        private async Task<List<TableViewModel>> GetTables(string type = "TABLE", string accountKey = null, string dataConnectKey = null)
        {
            var tables = new List<TableViewModel>();

            var currentTables = new List<TableViewModel>();

            if (!String.IsNullOrEmpty(accountKey) && !String.IsNullOrEmpty(dataConnectKey))
            {
                currentTables = await GetApiTables(accountKey, dataConnectKey);
            }

            var connString = await GetConnectionString(GetConnection(dataConnectKey));
            using (var conn = new MySqlConnection(connString))
            {
                // open the connection to the database 
                conn.Open();

                // Get the Tables
                var schemaTable = conn.GetSchema(type == "TABLE" ? "Tables" : "Views");
                var tablesToFilter = new List<string> { ""};

                // Store the table names in the class scoped array list of table names
                for (int i = 0; i < schemaTable.Rows.Count; i++)
                {
                    var tableName = schemaTable.Rows[i].ItemArray[2].ToString();
                    if (tablesToFilter.Any() && !tablesToFilter.Contains(tableName)) continue;

                    // see if this table is already in database
                    var matchTable = currentTables.FirstOrDefault(x => x.TableName.ToLower() == tableName.ToLower());
                    if (matchTable != null)
                    {
                        matchTable.Columns = await GetApiFields(accountKey, dataConnectKey, matchTable.Id);
                    }

                    var table = new TableViewModel
                    {
                        Id = matchTable != null ? matchTable.Id : 0,
                        TableName = matchTable != null ? matchTable.TableName : tableName,
                        DisplayName = matchTable != null ? matchTable.DisplayName : tableName,
                        IsView = type == "VIEW",
                        Selected = matchTable != null,
                        Columns = new List<ColumnViewModel>(),
                        AllowedRoles = matchTable != null ? matchTable.AllowedRoles : new List<string>()
                    };

                    var dtField = conn.GetSchema("Columns");
                    var idx = 0;

                    foreach (DataRow dr in dtField.Rows)
                    {
                        if (dr[2].ToString().ToLower() == table.TableName.ToLower())
                        {
                            ColumnViewModel matchColumn = matchTable != null ? matchTable.Columns.FirstOrDefault(x => x.ColumnName.ToLower() == dr["COLUMN_NAME"].ToString().ToLower()) : null;
                            var column = new ColumnViewModel
                            {
                                ColumnName = matchColumn != null ? matchColumn.ColumnName : dr["COLUMN_NAME"].ToString(),
                                DisplayName = matchColumn != null ? matchColumn.DisplayName : dr["COLUMN_NAME"].ToString(),
                                PrimaryKey = matchColumn != null ? matchColumn.PrimaryKey : dr["COLUMN_NAME"].ToString().ToLower().EndsWith("id") && idx == 0,
                                DisplayOrder = matchColumn != null ? matchColumn.DisplayOrder : idx++,
                                FieldType = matchColumn != null ? matchColumn.FieldType : ConvertMySqlDataTypeToFieldType(dr["DATA_TYPE"].ToString().ToLower()).ToString(),
                                AllowedRoles = matchColumn != null ? matchColumn.AllowedRoles : new List<string>()
                            };

                            if (matchColumn != null)
                            {
                                column.ForeignKey = matchColumn.ForeignKey;
                                column.ForeignJoin = matchColumn.ForeignJoin;
                                column.ForeignTable = matchColumn.ForeignTable;
                                column.ForeignKeyField = matchColumn.ForeignKeyField;
                                column.ForeignValueField = matchColumn.ForeignValueField;
                                column.Id = matchColumn.Id;
                                column.DoNotDisplay = matchColumn.DoNotDisplay;
                                column.DisplayOrder = matchColumn.DisplayOrder;
                                column.ForceFilter = matchColumn.ForceFilter;
                                column.Selected = true;
                            }

                            table.Columns.Add(column);
                        }
                    }
                    table.Columns = table.Columns.OrderBy(x => x.DisplayOrder).ToList();
                    tables.Add(table);
                }

                conn.Close();
                conn.Dispose();
            }


            return tables;
        }

        private async Task<List<TableViewModel>> GetApiProcs(string accountKey, string dataConnectKey)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(String.Format("{0}/ReportApi/GetProcedures?account={1}&dataConnect={2}&clientId=", Startup.StaticConfig.GetValue<string>("dotNetReport:apiUrl"), accountKey, dataConnectKey));
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var tables = JsonConvert.DeserializeObject<List<TableViewModel>>(content);

                return tables;
            }
        }

        private Type GetType(FieldTypes type)
        {
            switch (type)
            {
                case FieldTypes.Boolean:
                    return typeof(bool);
                case FieldTypes.DateTime:
                    return typeof(DateTime);
                case FieldTypes.Double:
                    return typeof(Double);
                case FieldTypes.Int:
                    return typeof(int);
                case FieldTypes.Money:
                    return typeof(decimal);
                case FieldTypes.Varchar:
                    return typeof(string);
                default:
                    return typeof(string);

            }
        }

        [HttpPost]
        public async Task<ActionResult> SearchProcedure([FromBody] dynamic data)
        {
            string value = data.value; string accountKey = data.accountKey; string dataConnectKey = data.dataConnectKey;
            return Json(await GetSearchProcedure(value, accountKey, dataConnectKey));
        }

        private async Task<List<TableViewModel>> GetSearchProcedure(string value = null, string accountKey = null, string dataConnectKey = null)
        {
            var tables = new List<TableViewModel>();
            var connString = await GetConnectionString(GetConnection(dataConnectKey));
            using (var conn = new MySqlConnection(connString))
            {
                // open the connection to the database 
                conn.Open();
                string spQuery = "SELECT ROUTINE_NAME, ROUTINE_DEFINITION, ROUTINE_SCHEMA FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_DEFINITION LIKE '%" + value + "%' AND ROUTINE_TYPE = 'PROCEDURE'";
                var cmd = new MySqlCommand(spQuery, conn);
                cmd.CommandType = CommandType.Text;
                DataTable dtProcedures = new DataTable();
                dtProcedures.Load(cmd.ExecuteReader());
                int count = 1;
                foreach (DataRow dr in dtProcedures.Rows)
                {
                    string procName = dr["ROUTINE_NAME"].ToString();
                    cmd = new MySqlCommand(procName, conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    // Get the parameters.
                    MySqlCommandBuilder.DeriveParameters(cmd);
                    List<ParameterViewModel> parameterViewModels = new List<ParameterViewModel>();
                    foreach (MySqlParameter param in cmd.Parameters)
                    {
                        if (param.Direction == ParameterDirection.Input)
                        {
                            var parameter = new ParameterViewModel
                            {
                                ParameterName = param.ParameterName,
                                DisplayName = param.ParameterName,
                                ParameterValue = param.Value != null ? param.Value.ToString() : "",
                                ParamterDataTypeOleDbTypeInteger = Convert.ToInt32(param.MySqlDbType),
                                ParameterDataTypeString = GetType(ConvertMySqlDataTypeToFieldType(param.MySqlDbType.ToString())).Name
                            };
                            if (parameter.ParameterDataTypeString.StartsWith("Int")) parameter.ParameterDataTypeString = "Int";
                            parameterViewModels.Add(parameter);
                        }
                    }
                    DataTable dt = new DataTable();
                    cmd = new MySqlCommand($"[{procName}]", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    foreach (var data in parameterViewModels)
                    {
                        cmd.Parameters.Add(new MySqlParameter { Value = DBNull.Value, ParameterName = data.ParameterName, Direction = ParameterDirection.Input, IsNullable = true });
                    }

                    var reader = cmd.ExecuteReader();
                    dt = reader.GetSchemaTable();

                    // Store the table names in the class scoped array list of table names
                    List<ColumnViewModel> columnViewModels = new List<ColumnViewModel>();
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        var column = new ColumnViewModel
                        {
                            ColumnName = dt.Rows[i].ItemArray[0].ToString(),
                            DisplayName = dt.Rows[i].ItemArray[0].ToString(),
                            FieldType = ConvertMySqlDataTypeToFieldType(dt.Rows[i]["ProviderType"].ToString()).ToString()
                        };
                        columnViewModels.Add(column);
                    }
                    tables.Add(new TableViewModel
                    {
                        TableName = procName,
                        SchemaName = dr["ROUTINE_SCHEMA"].ToString(),
                        DisplayName = procName,
                        Parameters = parameterViewModels,
                        Columns = columnViewModels
                    });
                    count++;
                }
                conn.Close();
                conn.Dispose();
            }
            return tables;
        }

        private async Task<DataTable> GetStoreProcedureResult(TableViewModel model, string accountKey = null, string dataConnectKey = null)
        {
            DataTable dt = new DataTable();
            var connString = await GetConnectionString(GetConnection(dataConnectKey));
            using (var conn = new MySqlConnection(connString))
            {
                // open the connection to the database 
                conn.Open();
                var cmd = new MySqlCommand(model.TableName, conn);
                cmd.CommandType = CommandType.StoredProcedure;
                foreach (var para in model.Parameters)
                {
                    if (string.IsNullOrEmpty(para.ParameterValue))
                    {
                        if (para.ParamterDataTypeOleDbType == SqlDbType.Timestamp || para.ParamterDataTypeOleDbType == SqlDbType.Date)
                        {
                            para.ParameterValue = DateTime.Now.ToShortDateString();
                        }
                    }
                    cmd.Parameters.AddWithValue("@" + para.ParameterName, para.ParameterValue);
                    //cmd.Parameters.Add(new OleDbParameter { 
                    //    Value =  string.IsNullOrEmpty(para.ParameterValue) ? DBNull.Value : (object)para.ParameterValue , 
                    //    ParameterName = para.ParameterName, 
                    //    Direction = ParameterDirection.Input, 
                    //    IsNullable = true });
                }
                dt.Load(cmd.ExecuteReader());
                conn.Close();
                conn.Dispose();
            }
            return dt;
        }

        #endregion
    }
}