using IDsys.Extensions.Attributes;

namespace IdSys.OneSheet.BusinessLogic.Enums
{
    public enum AutomaticActionRuleType
    {
        [TranslatableDescription(nameof(T.AutomaticActionRuleTypeRfidDeviceTagsProcess), typeof(T))]
        RfidDeviceTagsProcess = 1,

        [TranslatableDescription(nameof(T.AutomaticActionRuleTypeDataRowIsUpdated), typeof(T))]
        DataRowIsUpdated = 2,

        [TranslatableDescription(nameof(T.AutomaticActionRuleTypeOnDataRowLabelPrinted), typeof(T))]
        LabelPrinted = 3,
        
        [TranslatableDescription(nameof(T.AutomaticActionRuleTypeRfidSecurityGateAlarmFilter), typeof(T))]
        RfidSecurityGateAlarmFilter = 4,
        
        [TranslatableDescription(nameof(T.AutomaticActionRuleTypeRfidSecurityGateDisarm), typeof(T))]
        RfidSecurityGateDisarm = 5,

        [TranslatableDescription(nameof(T.TimeDatarowUpdate), typeof(T))]
        TimeDataRowUpdate = 6,
        
        [TranslatableDescription(nameof(T.AutomaticActionRuleTypeSelfServiceCheckInAndOut), typeof(T))]
        SelfServiceCheckInAndOut = 7,

        [TranslatableDescription(nameof(T.AutomaticActionRuleTypeSelfServiceCheckInAndOut), typeof(T))]
        Button = 8,
    }
}
