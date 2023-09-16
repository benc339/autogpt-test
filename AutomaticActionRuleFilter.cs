using IdSys.OneSheet.BusinessLogic.Enums;

namespace IdSys.OneSheet.BusinessLogic.Entities
{
    public  class AutomaticActionRuleFilter : EntityIdC
    {
        [ForeignKey("AutomaticActionRule")]
        public int AutomaticActionRuleId { get; set; }

        public AutomaticActionRule AutomaticActionRule { get; set; }
        
        public FilterType FilterType { get; set; }

        public string Filter { get; set; }
    }
}
