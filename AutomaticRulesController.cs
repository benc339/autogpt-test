using IDsys.Extensions.Extensions;
using IdSys.OneSheet.BusinessLogic;
using IdSys.OneSheet.BusinessLogic.Constants;
using IdSys.OneSheet.BusinessLogic.Entities;
using IdSys.OneSheet.BusinessLogic.Enums;
using IdSys.OneSheet.BusinessLogic.Services;
using IdSys.OneSheet.BusinessLogic.SyncManager;
using IdSys.OneSheet.Web.Extensions;
using IdSys.OneSheet.Web.Models.AutomaticRules;
using IDsys.SignalR.Shared.Enums;
using IDsys.SignalR.Shared.Extensions;
using IDsys.SignalR.Shared.Interfaces;
using IDsys.SignalR.Shared.Services;
using iTextSharp.text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MvcGrid.AspNetCore;
using MvcGrid.AspNetCore.Models;
using Org.BouncyCastle.Utilities;
using System;
using System.Text.RegularExpressions;
using GoogleApi.Entities.Maps.Common;

namespace IdSys.OneSheet.Web.Controllers
{
    [Authorize(Roles = Roles.SuperAdmin)]
    public class AutomaticRulesController : BaseController
    {
        private readonly IDeviceControlService deviceControlService;
        private readonly IDeviceConfigurationService deviceConfigurationService;
        private readonly ILogger<AutomaticRulesController> logger;
        private readonly ApplicationDbContext context;
        private readonly IDataGridHelperService dataGridHelperService;
        private readonly ICustomerDatabaseService customerDatabaseService;
        private readonly ICurrentUserService customerUserService;
        private readonly IAgGridColumnService agGridColumnService;
        private readonly ModelExpressionProvider expressionProvider;

        public AutomaticRulesController(IDeviceControlService deviceControlService, IDeviceConfigurationService deviceConfigurationService, ILogger<AutomaticRulesController> logger, ApplicationDbContext context, IDataGridHelperService dataGridHelperService,
            ICustomerDatabaseService customerDatabaseService, ICurrentUserService customerUserService, IAgGridColumnService agGridColumnService, UserManager<User> userManager, RoleManager<Role> roleManager, ModelExpressionProvider expressionProvider) : base(userManager, roleManager)
        {
            this.deviceControlService = deviceControlService;
            this.deviceConfigurationService = deviceConfigurationService;
            this.logger = logger;
            this.context = context;
            this.dataGridHelperService = dataGridHelperService;
            this.customerDatabaseService = customerDatabaseService;
            this.customerUserService = customerUserService;
            this.agGridColumnService = agGridColumnService;
            this.expressionProvider = expressionProvider;
        }

        #region Search

        [HttpGet]
        public IActionResult Index(int? customerId, string status)
        {
            customerId = customerUserService.GetCustomerId(customerId);

            var model = new IndexModel
            {
                CustomerId = customerId,
                Status = { SelectedValue = status },
                Customer = context.Customers.FirstOrDefault(x => x.Id == customerId)
            };

            return View(model);
        }

        internal static void RegisterAutomaticRulesGrid(MvcGridMiddlewareOptions options, GridDefaults gridDefaults, ColumnDefaults colDefaults)
        {
            options.Add("AutomaticRulesGrid", new MVCGridBuilder<AutomaticActionRule>(gridDefaults, colDefaults)
                .WithAuthorizationType(AuthorizationType.Authorized)
                .WithRoles(Roles.SuperAdmin)
                .WithDefaultSortColumn("Name")
                .WithPageParameterNames("CustomerId")
                .WithDefaultSortDirection(SortDirection.Asc)
                .AddColumns(cols =>
                {
                    cols.Add().WithColumnName("Name")
                        .WithHeaderCssClass("col-4")
                        .WithHeaderTextExpression(() => T.Name2)
                        .WithValueExpression(i => i.Name)
                        .WithSorting(true);

                    cols.Add().WithColumnName("TypeField")
                        .WithHeaderCssClass("col-4")
                        .WithHeaderTextExpression(() => T.Type)
                        .WithValueExpression(i => i.Type.GetDescriptionValue())
                        .WithSorting(true);

                    cols.Add().WithColumnName("Status")
                        .WithHeaderCssClass("col-2")
                        .WithCellCssClassExpression(x => "")
                        .WithHeaderTextExpression(() => T.Status)
                        .WithValueExpression(i => i.Enabled ? T.Active : T.Archived).WithFiltering(true);

                    cols.Add().WithColumnName("Actions")
                        .WithHeaderCssClass("col")
                        .WithHeaderText("")
                        .WithCellCssClassExpression(_ => "actions")
                        .WithValueExpression((x, c) => c.RenderPartial("MvcGridActions/AutomaticRulesActions", x))
                        .WithHtmlEncoding(false)
                        .WithSorting(false);
                })
                .WithFiltering(true)
                .WithRetrieveDataMethod(context => context.CurrentHttpContext.RequestServices.GetRequiredService<IMvcGridViewService>().SearchAutomaticActionRules(context.QueryOptions))
            );
        }

        #endregion

        #region Add & Edit

        public async Task<IActionResult> Edit(EditModel model)
        {
            if (!User.IsInRole(Roles.SuperAdmin)) model.CustomerId = CurrentUser.CustomerId;
            model.Customer = context.Customers.FirstOrDefault(x => x.Id == model.CustomerId);
            model.ExtraFields = dataGridHelperService.GetExtraFieldNameModel(model.CustomerId);

            var customerContext = customerDatabaseService.GetContext(model.CustomerId);

            ViewData["Columns"] = model.CustomerId == null ? null : agGridColumnService.GetDataRowColumnNames(model.CustomerId);
            ViewData["DeliveryColumns"] = model.CustomerId == null ? null : agGridColumnService.DeliveryColumns.ToDictionary(x => x.DataFiled, x => x.HeaderName);

            if (Request.Method == "GET")
            {
                var automaticRule = context.AutomaticActionRules
                    .Include(x => x.Actions)
                    .Include(x => x.Filters)
                    .FirstOrDefault(x => x.Id == model.Id && x.CustomerId == model.CustomerId);

                model = model.FillData(automaticRule);
                model.FillInfo(context, customerContext);

                ModelState.Clear();

                return View(model);
            }

            model.FillInfo(context, customerContext);

            #region SubActions

            switch (model.SubAction)
            {
                case AutomaticRuleSubActionType.Update:
                case AutomaticRuleSubActionType.FieldTypeChange:
                case AutomaticRuleSubActionType.TypeChange:
                    ModelState.Clear();
                    model.SubAction = AutomaticRuleSubActionType.None;
                    return PartialView(model);

                case AutomaticRuleSubActionType.AddRow:
                    var group = model.AutomaticRuleFilters.FirstOrDefault(x => x.AddFilter);
                    if (group != null)
                    {
                        group.AddFilter = false;
                        group.Filters.Add(new AutomaticRuleFilterModel());
                    }
                    var actionGroup = model.AutomaticRuleActions.FirstOrDefault(x => x.AddAction);
                    if (actionGroup != null)
                    {
                        actionGroup.AddAction = false;
                        actionGroup.Actions.Add(new AutomaticRuleActionModel());
                    }
                    ModelState.Clear();
                    model.SubAction = AutomaticRuleSubActionType.None;
                    return PartialView(model);

                case AutomaticRuleSubActionType.RemoveRow:
                    model.AutomaticRuleFilters.ForEach(x => x.Filters = x.Filters.Where(y => !y.Delete).ToList());
                    model.AutomaticRuleActions.ForEach(x => x.Actions = x.Actions.Where(y => !y.Delete).ToList());
                    ModelState.Clear();
                    return PartialView(model);
            }

            #endregion

            #region Validate

            model.ValidateTrue(x => x.Name, !context.CustomerDeviceConfigurations.Any(x => x.Id != model.Id && x.Name == model.Name), expressionProvider, ModelState, T.AutomaticActionRuleNameUnique);
            model.ValidateNotSelected(x => x.Type.SelectedValue, expressionProvider, ModelState, T.ValidationFieldIsRequired);

            var validationFields = model.Type.SelectedValue != null && AutomaticActionRule.AutomaticActionRuleTypeFields.Any(x => (int)x.Key == model.Type.SelectedValue) ? AutomaticActionRule.AutomaticActionRuleTypeFields[(AutomaticActionRuleType)model.Type.SelectedValue.Value] : new List<AutomaticActionRuleFieldType>();

            foreach (var validationField in validationFields)
            {
                switch (validationField)
                {
                    case AutomaticActionRuleFieldType.FilterOperator:
                        model.ValidateNotSelected(x => x.FilterOperator.SelectedValue, expressionProvider, ModelState, T.AutomaticRuleFilterOperatorNeeded);
                        break;
                    case AutomaticActionRuleFieldType.AntennaFilter:
                        if (model.AntennaFilterEnabled)
                            model.ValidateTrue(x => x.AntennaFilterEnabled, model.Antenna0Selected || model.Antenna1Selected || model.Antenna2Selected || model.Antenna3Selected, expressionProvider, ModelState, T.AutomaticRulesEditOneAntennaMustBeSelected);
                        break;
                    case AutomaticActionRuleFieldType.TimeFilter:
                        model.ValidateTrue(x => x.TimeFilterFormula, model.TimeFilterFormula == null || model.TimeFilterFormula.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).All(x => Regex.IsMatch(x, "^(([0-9]{1,})(min|h|d|m|y))$", RegexOptions.IgnoreCase)), expressionProvider, ModelState, T.TimeFilterFormulaValidate);
                        break;
                    case AutomaticActionRuleFieldType.TimeCheck:
                        model.ValidateTrue(x => x.CheckTime, model.CheckTime != null || model.CheckTime <= 0, expressionProvider, ModelState, T.AutomaticRulesCheckTimeValidation);
                        break;
                }
                
            }
            
            #region Validate Filters

            if (validationFields.Any(x => model.ActionTypes.Contains(x)))
            {
                for (var i = 0; i < model.AutomaticRuleFilters.Count; i++)
                {
                    for (var ii = 0; ii < model.AutomaticRuleFilters[i].Filters.Count; ii++)
                    {
                        switch (model.AutomaticRuleFilters[i].Type)
                        {
                            case FilterType.None:
                                break;
                            case FilterType.DataRow:
                            case FilterType.Delivery:
                            case FilterType.Inventory:
                            case FilterType.Order:
                                model.ValidateNotNull(x => x.AutomaticRuleFilters[i].Filters[ii].Filter, expressionProvider, ModelState);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
            }

            #endregion

            #region Validate Actions

            if (validationFields.Any(x => model.ActionTypes.Contains(x)))
            {
                for (var i = 0; i < model.AutomaticRuleActions.Count; i++)
                {
                    for (var ii = 0; ii < model.AutomaticRuleActions[i].Actions.Count; ii++)
                    {
                        switch (model.AutomaticRuleActions[i].Type)
                        {
                            case ActionType.UpdateDataRow:

                                model.ValidateNotSelected(x => x.AutomaticRuleActions[i].Actions[ii].SelectedField, expressionProvider, ModelState);
                                if (model.AutomaticRuleActions[i].Actions[ii].IsMetaItem) model.ValidateNotSelected(x => x.AutomaticRuleActions[i].Actions[ii].MetaItemId, expressionProvider, ModelState);

                                break;
                            case ActionType.UpdateDevicesGpioState:
                                model.ValidateNotSelected(x => x.AutomaticRuleActions[i].Actions[ii].Gpio, expressionProvider, ModelState);
                                model.ValidateNotSelected(x => x.AutomaticRuleActions[i].Actions[ii].GpioState, expressionProvider, ModelState);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
            }

            #endregion


            #endregion

            #region Save

            if (ModelState.IsValid)
            {
                try
                {


                    var ar = context.AutomaticActionRules.Include(x => x.Filters).Include(x => x.Actions).FirstOrDefault(x => x.Id == model.Id) ?? context.AddNew<AutomaticActionRule>();
                    var isNewDevice = ar.Id == 0;
                    ar.Customer = model.Customer;
                    ar.Name = model.Name;
                    ar.Notes = model.Notes;
                    ar.Type = (AutomaticActionRuleType)model.Type.SelectedValue.GetValueOrDefault();
                    ar.Enabled = model.Enabled;
                    ar.AntennaFilter = null;
                    ar.Priority = model.Priority.GetValueOrDefault(1);

                    var saveFields = AutomaticActionRule.AutomaticActionRuleTypeFields[ar.Type];

                    foreach (var saveField in saveFields)
                    {
                        switch (saveField)
                        {
                            case AutomaticActionRuleFieldType.FilterOperator:
                                ar.FilterOperator = (FilterOperatorType)model.FilterOperator.SelectedValue.GetValueOrDefault();
                                break;
                            case AutomaticActionRuleFieldType.AntennaFilter:
                                if (model.AntennaFilterEnabled)
                                {
                                    ar.AntennaFilter = RfidAntennaType.None;
                                    if (model.Antenna0Selected) ar.AntennaFilter |= RfidAntennaType.Ant0;
                                    if (model.Antenna1Selected) ar.AntennaFilter |= RfidAntennaType.Ant1;
                                    if (model.Antenna2Selected) ar.AntennaFilter |= RfidAntennaType.Ant2;
                                    if (model.Antenna3Selected) ar.AntennaFilter |= RfidAntennaType.Ant3;
                                }
                                break;
                            case AutomaticActionRuleFieldType.DataRowFilter:
                            case AutomaticActionRuleFieldType.ActionUpdateDataRow:
                            case AutomaticActionRuleFieldType.ActionUpdateDevicesGpioState:
                                break;
                            case AutomaticActionRuleFieldType.TimeFilter:
                                ar.TimeFilterFormula = model.TimeFilterFormula;
                                break;
                            case AutomaticActionRuleFieldType.TimeCheck:
                                ar.CheckTime = model.CheckTime;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                    #region Filters

                    var existingFilters = ar.Filters.ToList();

                    if (saveFields.Any(x => model.FilterTypes.Contains(x)))
                    {
                        foreach (var group in model.AutomaticRuleFilters)
                        {
                            foreach (var filter in group.Filters)
                            {
                                var dbFilter = existingFilters.FirstOrDefault(x => x.Id == filter.Id) ?? context.AddNew<AutomaticActionRuleFilter>();

                                if (existingFilters.Contains(dbFilter)) existingFilters.Remove(dbFilter);

                                dbFilter.FilterType = group.Type;
                                dbFilter.AutomaticActionRule = ar;
                                dbFilter.FilterType = group.Type;
                                dbFilter.Filter = filter.Filter;
                            }
                        }
                    }

                    existingFilters.ForEach(x => context.Remove(x));

                    #endregion

                    #region Actions

                    var existingActions = ar.Actions.ToList();

                    if (saveFields.Any(x => model.ActionTypes.Contains(x)))
                    {
                        foreach (var group in model.AutomaticRuleActions)
                        {
                            foreach (var actionModel in group.Actions)
                            {
                                var dbAction = existingActions.FirstOrDefault(x => x.Id == actionModel.Id) ?? context.AddNew<AutomaticActionRuleAction>();

                                if (existingActions.Contains(dbAction)) existingActions.Remove(dbAction);

                                dbAction.Type = group.Type;
                                dbAction.AutomaticActionRule = ar;
                                dbAction.Field = (OnlineFieldType?)actionModel.SelectedField;
                                dbAction.MetaItemId = null;
                                dbAction.Format = null;
                                dbAction.Gpio = null;
                                dbAction.GpioState = null;

                                switch (group.Type)
                                {
                                    case ActionType.UpdateDataRow:
                                        if (actionModel.IsMetaItem) dbAction.MetaItemId = actionModel.MetaItemId;
                                        else dbAction.Format = actionModel.Format;
                                        break;
                                    case ActionType.UpdateDevicesGpioState:
                                        dbAction.Gpio = (GpioType)actionModel.Gpio;
                                        dbAction.GpioState = (GpioStateType)actionModel.GpioState;
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                        }
                    }

                    existingActions.ForEach(x => context.Remove(x));

                    #endregion

                    await context.SaveChangesAsync();

                    model.Id = ar.Id;
                    ModelState.Clear();
                    ViewBag.Result = isNewDevice ? T.AutomaticActionRuleSaveFailed : T.ChangesSaved;
                }
                catch (Exception e)
                {
                    ModelState.AddModelError(string.Empty, T.AutomaticActionRuleSaveFailed);
                    logger.LogError(e, "Error saving automatic rule");
                }
            }

            #endregion

            return PartialView(model);
        }

        #endregion

        #region Delete

        [HttpGet]
        public IActionResult Delete(int id, int? customerId)
        {
            customerId = customerUserService.GetCustomerId(customerId);

            try
            {
                var automaticRule = context.AutomaticActionRules
                    .Include(x => x.CustomerDeviceConfigurationRules)
                    .Include(x => x.Filters)
                    .Include(x => x.Actions)

                    .FirstOrDefault(x => x.Id == id && x.CustomerId == customerId);

                if (automaticRule != null)
                {
                    automaticRule.CustomerDeviceConfigurationRules.ForEach(x => context.Remove(x));
                    automaticRule.Filters.ForEach(x => context.Remove(x));
                    automaticRule.Actions.ForEach(x => context.Remove(x));

                    context.AutomaticActionRules.Remove(automaticRule);
                    context.SaveChanges();
                }

                return Json(new { Result = true, Message = T.DeviceDeleted });
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred when deleting an automatic rule.");
                return Json(new { Result = false, Message = T.AutomaticRuleDeletedErrorMessage });
            }
        }

        #endregion

    }
}
