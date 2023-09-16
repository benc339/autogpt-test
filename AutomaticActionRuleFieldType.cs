using IDsys.Extensions.Attributes;

namespace IdSys.OneSheet.BusinessLogic.Enums
{
    public enum AutomaticActionRuleFieldType
    {
        [TranslatableDescription(nameof(T.AutomaticActionRuleFieldTypeFilterCombiningMode), typeof(T))]
        FilterOperator,

        [TranslatableDescription(nameof(T.AutomaticActionRuleFieldTypeDataRowsFilter), typeof(T))]
        DataRowFilter,

        [TranslatableDescription(nameof(T.AutomaticActionRuleFieldTypeUpdateDataRowFiled), typeof(T))]
        ActionUpdateDataRow,

        [TranslatableDescription(nameof(T.AutomaticActionRuleFieldTypeSendGPIOStateToDevice), typeof(T))]
        ActionUpdateDevicesGpioState,

        [TranslatableDescription(nameof(T.AutomaticActionRuleFieldTypeAntennaFilter), typeof(T))]
        AntennaFilter,

        [TranslatableDescription(nameof(T.AutomaticActionRuleFieldTypeTimeFilter), typeof(T))]
        TimeFilter,

        [TranslatableDescription(nameof(T.AutomaticActionRuleFieldTypeCheckTime), typeof(T))]
        TimeCheck,
    }
}
