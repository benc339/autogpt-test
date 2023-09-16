using IdSys.OneSheet.BusinessLogic.Constants;
using IdSys.OneSheet.BusinessLogic.Enums;
using IdSys.OneSheet.BusinessLogic.SyncManager;
using IDsys.SignalR.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace IdSys.OneSheet.BusinessLogic.Entities
{
    public class AutomaticActionRuleAction : EntityIdC
    {
        [ForeignKey("AutomaticActionRule")]
        public int AutomaticActionRuleId { get; set; }

        public AutomaticActionRule AutomaticActionRule { get; set; }

        public ActionType Type { get; set; }

        public OnlineFieldType? Field { get; set; }

        public int? MetaItemId { get; set; }

        public string Format { get; set; }

        public GpioType? Gpio { get; set; }

        public GpioStateType? GpioState { get; set; }

        public string GetFormattedValue(CustomerDeviceConfiguration deviceConfiguration = null, CustomerCustomView customerCustomView = null, DataRow dataRow = null, MetaItem metaItem = null)
        {
            var format = Format
                .Replace("{datetime_now}", $"{DateTime.UtcNow:dd.MM.yyyy HH:mm:ss}")
                .Replace("{device_id}", deviceConfiguration?.Id.ToString())
                .Replace("{device_name}", deviceConfiguration?.Name)
                .Replace("{device_host}", deviceConfiguration?.Host)
                .Replace("{device_port}", deviceConfiguration?.Port.ToString())
                .Replace("{device_type}", deviceConfiguration?.Type.ToString())
                .Replace("{view_id}", customerCustomView?.Id.ToString())
                .Replace("{view_name}", customerCustomView?.Name)
                .Replace("{view_type}", customerCustomView?.Type.ToString())
                .Replace("{worker_name}", metaItem?.Name)
                .Replace("{worker_code}", metaItem?.Code);

            if (dataRow != null)
            {
                var onlineProvider = new OnlineProvider();

                foreach (var onlineFieldType in Enum.GetValues<OnlineFieldType>())
                {
                    var value = string.Empty;

                    if (onlineProvider.AvailableFields.ContainsKey((int)onlineFieldType))
                    {
                        var field = onlineProvider.AvailableFields[(int)onlineFieldType];
                        value = field.MapObjectIn(dataRow)?.ToString();
                    }

                    format = format.Replace($"{{asset_field:{onlineFieldType}}}", value);
                }
            }

            return format;
        }

        public void Apply(CustomerDbContext customerDbContext, ILogger logger, DataRow dr,
            OnlineProvider onlineProvider, List<int> changedDataRowIds, DataRow oldDataRow = null, MetaItem worker = null,
            CustomerDeviceConfiguration deviceConfiguration = null, CustomerCustomView customView = null)
        {
            if (Field == null || !onlineProvider.AvailableFields.ContainsKey((int)Field)) return;
            var field = onlineProvider.AvailableFields[(int)Field];

            if (MetaItemId != null)
            {
                //Siin peab MetaItem'iga arvestama :)
                var metaItem = customerDbContext.MetaItems.FirstOrDefault(x => x.Id == MetaItemId);

                if (metaItem == null)
                {
                    logger.LogError($"MetaItem with id {MetaItemId} not found, skipping AutomaticActionRuleAction");
                    return;
                }

                var table = MetaTableInfo.Tables.First(x => x.TypeId == metaItem.TypeId);

                field.MapObjectOut(dr, table.ValueFormat != null ? table.ValueFormat(metaItem) : metaItem.Name);
                field.MapMetaItem(dr, metaItem.Id);
            }
            else
            {
                field.MapObjectOut(dr, GetFormattedValue(deviceConfiguration, customView, oldDataRow, worker));
            }

            dr.ModifiedTime = DateTime.UtcNow;
            dr.ModifiedPersonId = Person.SystemPersonId;

            if (!changedDataRowIds.Contains(dr.Id)) changedDataRowIds.Add(dr.Id);
        }
    }
}
