using IDsys.Extensions.Attributes;
using IDsys.Extensions.Extensions;
using IdSys.OneSheet.BusinessLogic;
using IdSys.OneSheet.BusinessLogic.Constants;
using IdSys.OneSheet.BusinessLogic.Entities;
using IdSys.OneSheet.BusinessLogic.Enums;
using IdSys.OneSheet.BusinessLogic.Models.Data;
using IdSys.OneSheet.BusinessLogic.SyncManager;
using IdSys.OneSheet.Web.Validation;
using IDsys.SignalR.Shared.Enums;
using IDsys.SignalR.Shared.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using NaturalSort.Extension;
using Enum = System.Enum;
using Google.Protobuf.WellKnownTypes;
using iTextSharp.text;

namespace IdSys.OneSheet.Web.Models.AutomaticRules
{
    public class EditModel
    {
        public EditModel() { }

        public EditModel FillData(AutomaticActionRule ar)
        {
            if (ar == null) return this;

            this.InjectFromCustom(ar);
            Type.SelectedValue = (int)ar.Type;
            Priority = ar.Priority;

            foreach (var fieldType in AutomaticActionRule.AutomaticActionRuleTypeFields[ar.Type])
            {
                switch (fieldType)
                {
                    case AutomaticActionRuleFieldType.FilterOperator:
                        FilterOperator.SelectedValue = (int)ar.FilterOperator;
                        break;
                    case AutomaticActionRuleFieldType.DataRowFilter:
                        var drFilter = AutomaticRuleFilters.AddNew(x => x.Type = FilterType.DataRow);
                        drFilter.Filters = ar.Filters.Where(x => x.FilterType == FilterType.DataRow).Select(x => new AutomaticRuleFilterModel { Id = x.Id, Filter = x.Filter }).ToList();
                        break;
                    case AutomaticActionRuleFieldType.ActionUpdateDataRow:
                        var audrAction = AutomaticRuleActions.AddNew(x => x.Type = ActionType.UpdateDataRow);
                        audrAction.Actions = ar.Actions.Where(x => x.Type == ActionType.UpdateDataRow).Select(x => new AutomaticRuleActionModel(x)).ToList();
                        break;
                    case AutomaticActionRuleFieldType.ActionUpdateDevicesGpioState:
                        var audgsAction = AutomaticRuleActions.AddNew(x => x.Type = ActionType.UpdateDevicesGpioState);
                        audgsAction.Actions = ar.Actions.Where(x => x.Type == ActionType.UpdateDevicesGpioState).Select(x => new AutomaticRuleActionModel(x)).ToList();
                        break;
                    case AutomaticActionRuleFieldType.AntennaFilter:

                        AntennaFilterEnabled = ar.AntennaFilter != null;
                        Antenna0Selected = (ar.AntennaFilter & RfidAntennaType.Ant0) == RfidAntennaType.Ant0;
                        Antenna1Selected = (ar.AntennaFilter & RfidAntennaType.Ant1) == RfidAntennaType.Ant1;
                        Antenna2Selected = (ar.AntennaFilter & RfidAntennaType.Ant2) == RfidAntennaType.Ant2;
                        Antenna3Selected = (ar.AntennaFilter & RfidAntennaType.Ant3) == RfidAntennaType.Ant3;
                        break;
                    case AutomaticActionRuleFieldType.TimeFilter:
                        TimeFilterFormula = ar.TimeFilterFormula;
                        break;
                    case AutomaticActionRuleFieldType.TimeCheck:
                        CheckTime = ar.CheckTime;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return this;
        }
        public void FillInfo(ApplicationDbContext context, CustomerDbContext customerContext)
        {
            var type = (AutomaticActionRuleType?)Type.SelectedValue;

            var fields = type != null && AutomaticActionRule.AutomaticActionRuleTypeFields.ContainsKey(type.Value) ? AutomaticActionRule.AutomaticActionRuleTypeFields[type.Value] : new List<AutomaticActionRuleFieldType>();

            var existingAutomaticRuleFilters = AutomaticRuleFilters.ToList();
            var existingAutomaticRuleActions = AutomaticRuleActions.ToList();

            foreach (var automaticActionRuleFieldType in fields)
            {
                switch (automaticActionRuleFieldType)
                {
                    case AutomaticActionRuleFieldType.FilterOperator:
                        break;
                    case AutomaticActionRuleFieldType.DataRowFilter:
                        var ar = AutomaticRuleFilters.FirstOrDefault(x => x.Type == FilterType.DataRow) ?? AutomaticRuleFilters.AddNew(x => { x.Type = FilterType.DataRow; });
                        if (existingAutomaticRuleFilters.Contains(ar)) existingAutomaticRuleFilters.Remove(ar);
                        break;
                    case AutomaticActionRuleFieldType.ActionUpdateDataRow:
                        var audr = AutomaticRuleActions.FirstOrDefault(x => x.Type == ActionType.UpdateDataRow) ?? AutomaticRuleActions.AddNew(x => { x.Type = ActionType.UpdateDataRow; });
                        if (existingAutomaticRuleActions.Contains(audr)) existingAutomaticRuleActions.Remove(audr);
                        break;
                    case AutomaticActionRuleFieldType.ActionUpdateDevicesGpioState:
                        var audgs = AutomaticRuleActions.FirstOrDefault(x => x.Type == ActionType.UpdateDevicesGpioState) ?? AutomaticRuleActions.AddNew(x => { x.Type = ActionType.UpdateDevicesGpioState; });
                        if (existingAutomaticRuleActions.Contains(audgs)) existingAutomaticRuleActions.Remove(audgs);
                        break;
                }
            }

            existingAutomaticRuleFilters.ForEach(x => AutomaticRuleFilters.Remove(x));
            existingAutomaticRuleActions.ForEach(x => AutomaticRuleActions.Remove(x));

            #region Meta values

            var fieldMetaTypes = Customer.AllMetaItemType();
            var metaTypes = fieldMetaTypes.Select(x => x.MetaItemTypeId).ToArray();
            var metaItems = customerContext.MetaItems.Where(x => x.Deleted == null && metaTypes.Contains(x.TypeId)).ToList();

            foreach (var fieldMetaType in fieldMetaTypes)
            {
                AutomaticRuleActions.SelectMany(x => x.Actions).Where(x => x.SelectedField == (int)fieldMetaType.Field).ForEach(x => x.IsMetaItem = true);
                MetaItems.Add((int)fieldMetaType.Field, new List<SelectListItem>() { new SelectListItem($"--- {T.AutomaticRuleSelectMetaItem} ---", "-1") }.Concat(metaItems.Where(x => x.TypeId == fieldMetaType.MetaItemTypeId).Select(x => new SelectListItem(MetaTableInfo.FormattedValue(x), x.Id.ToString()))).ToList());
            }

            #endregion

            FieldTypes = new List<SelectListItem>() { new($"--- {T.AutomaticRuleSelectField} ---", "-1") }
                .Concat(Enum.GetValues<OnlineFieldType>().Where(x => !new[] { OnlineFieldType.AcquisitionDate, OnlineFieldType.Closed, OnlineFieldType.ClosedTime, OnlineFieldType.CloserPerson,
                        OnlineFieldType.ExternalId, OnlineFieldType.DeletedPerson, OnlineFieldType.DeletedTime, OnlineFieldType.InventoryNotes,
                        OnlineFieldType.InventoryTime, OnlineFieldType.LastReadPerson, OnlineFieldType.LastReadTime, OnlineFieldType.InventoryPerson,
                        OnlineFieldType.Rfid, OnlineFieldType.ModifiedPerson, OnlineFieldType.ModifiedTime }.Contains(x))
                    .Select(x => new SelectListItem(ExtraFields.GetName(x), ((int)x).ToString())).OrderBy(x => x.Text, StringComparison.CurrentCultureIgnoreCase.WithNaturalSort())).ToList();
        }

        public Customer Customer { get; set; }

        public int? Id { get; set; }

        public int? CustomerId { get; set; }

        public int? CurrentIndex { get; set; }

        public AutomaticRuleSubActionType SubAction { get; set; }

        public List<SelectListItem> AutomaticRuleItems { get; set; } = new();

        [Display(Name = nameof(T.AutomaticRuleEnabled), ResourceType = typeof(T))]
        public bool Enabled { get; set; } = true;

        [LocRequired]
        [StringLength(255, ErrorMessageResourceName = "HandheldNameLengthError", ErrorMessageResourceType = typeof(T))]
        [Display(Name = nameof(T.AutomaticRuleName), ResourceType = typeof(T))]
        public string Name { get; set; }

        [Display(Name = nameof(T.AutomaticRuleNotes), ResourceType = typeof(T))]
        public string Notes { get; set; }
        
        [Display(Name = nameof(T.AutomaticActionRuleFieldTypeTimeFilter), ResourceType = typeof(T))]
        [TranslatableInfo(ResourceName = nameof(T.AutomticRuleTimeFilterFormulInfo), ResourceType = typeof(T))]
        public string TimeFilterFormula { get; set; }

        [Display(Name = nameof(T.AutomaticActionRuleFieldTypeCheckTime), ResourceType = typeof(T))]
        [TranslatableInfo(ResourceName = nameof(T.AutomaticRuleCheckTimeInfo), ResourceType = typeof(T))]
        public int? CheckTime { get; set; }

        [Display(Name = nameof(T.AutomaticRulePriority), ResourceType = typeof(T))]
        [TranslatableInfo(ResourceName = nameof(T.AutomaticRulePriorityInfo), ResourceType = typeof(T))]
        public int? Priority { get; set; }

        [LocRequired]
        [Display(Name = nameof(T.AutomaticRuleType), ResourceType = typeof(T))]
        public DropDownListViewModel Type { get; set; } = new DropDownListViewModel
        {
            Items = new List<SelectListItem> { new SelectListItem($"--- {T.AutomaticRuleFieldTypeSearchMode} ---", "-1") }
                .Concat(Enum.GetValues<AutomaticActionRuleType>().Select(x => new SelectListItem(x.GetDescriptionValue(), ((int)x).ToString())).OrderBy(x => x.Text)).ToList()
        };

        [Display(Name = nameof(T.AutomaticRuleFilterOperator), ResourceType = typeof(T))]
        public DropDownListViewModel FilterOperator { get; set; } = new DropDownListViewModel
        {
            Items = new List<SelectListItem> { new SelectListItem($"--- {T.AutomaticRuleSelectFilterOperator} ---", "-1") }
                .Concat(Enum.GetValues<FilterOperatorType>().Select(x => new SelectListItem(x.GetDescriptionValue(), ((int)x).ToString())).OrderBy(x => x.Text)).ToList()
        };

        public List<SelectListItem> FieldTypes { get; set; }

        public List<SelectListItem> GpioTypes { get; set; } = new List<SelectListItem>() { new SelectListItem($"--- {T.AutomaticRuleSelectGpioType} ---", "-1") }
            .Concat(Enum.GetValues<GpioType>().Where(x => new[] { GpioType.Gpio1, GpioType.Gpio2, GpioType.Gpio3, GpioType.Gpio4 }.Contains(x)).Select(x => new SelectListItem(x.GetDescriptionValue(), ((int)x).ToString())).OrderBy(x => x.Text)).ToList();

        public List<SelectListItem> GpioStateTypes { get; set; } = new List<SelectListItem>() { new SelectListItem($"--- {T.AutomaticRuleSelectGpioStateType} ---", "-1") }
            .Concat(Enum.GetValues<GpioStateType>().Select(x => new SelectListItem(x.GetDescriptionValue(), ((int)x).ToString())).OrderBy(x => x.Text)).ToList();


        [Display(Name = nameof(T.AutomaticRuleAntennaEnabled), ResourceType = typeof(T))]
        public bool AntennaFilterEnabled { get; set; }

        [Display(Name = nameof(T.AutomaticRuleAntenna0Selected), ResourceType = typeof(T))]
        public bool Antenna0Selected { get; set; }

        [Display(Name = nameof(T.AutomaticRuleAntenna1Selected), ResourceType = typeof(T))]
        public bool Antenna1Selected { get; set; }

        [Display(Name = nameof(T.AutomaticRuleAntenna2Selected), ResourceType = typeof(T))]
        public bool Antenna2Selected { get; set; }

        [Display(Name = nameof(T.AutomaticRuleAntenna3Selected), ResourceType = typeof(T))]
        public bool Antenna3Selected { get; set; }


        public List<AutomaticRuleFilterGroupModel> AutomaticRuleFilters { get; set; } = new();

        public List<AutomaticRuleActionGroupModel> AutomaticRuleActions { get; set; } = new();

        public Dictionary<int, List<SelectListItem>> MetaItems { get; set; } = new();

        public AutomaticActionRuleFieldType[] FilterTypes => new[] { AutomaticActionRuleFieldType.DataRowFilter };

        public AutomaticActionRuleFieldType[] ActionTypes => new[] { AutomaticActionRuleFieldType.ActionUpdateDataRow, AutomaticActionRuleFieldType.ActionUpdateDevicesGpioState };

        public ExtraFieldNameModel ExtraFields { get; set; }

        public AutomaticActionRuleType AutomaticRuleType => (AutomaticActionRuleType)Type.SelectedValue.GetValueOrDefault();
    }

    public class AutomaticRuleFilterGroupModel
    {
        public bool AddFilter { get; set; }

        public FilterType Type { get; set; }

        public List<AutomaticRuleFilterModel> Filters { get; set; } = new List<AutomaticRuleFilterModel>();
    }

    public class AutomaticRuleFilterModel
    {
        public int Id { get; set; }

        public string Filter { get; set; }

        public bool Delete { get; set; }
    }


    public class AutomaticRuleActionGroupModel
    {
        public bool AddAction { get; set; }

        public ActionType Type { get; set; }

        public List<AutomaticRuleActionModel> Actions { get; set; } = new List<AutomaticRuleActionModel>();
    }

    public class AutomaticRuleActionModel
    {
        public AutomaticRuleActionModel()
        {

        }

        public AutomaticRuleActionModel(AutomaticActionRuleAction obj)
        {
            this.InjectFromCustom(obj);

            SelectedField = (int?)obj.Field;
            MetaItemId = obj.MetaItemId;
            Gpio = ((int?)obj.Gpio).GetValueOrDefault();
            GpioState = ((int?)obj.GpioState).GetValueOrDefault();
        }

        public int Id { get; set; }

        public int? SelectedField { get; set; }

        public int? MetaItemId { get; set; }

        public string Format { get; set; }

        public int? Gpio { get; set; }

        public int? GpioState { get; set; }

        public bool Delete { get; set; }
        public bool IsMetaItem { get; set; }
    }


    public enum AutomaticRuleSubActionType
    {
        None = 0,
        TypeChange = 1,
        AddRow = 2,
        RemoveRow = 3,
        Update = 6,
        FieldTypeChange = 7,
    }
}
