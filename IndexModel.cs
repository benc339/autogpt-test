using IdSys.OneSheet.BusinessLogic.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IdSys.OneSheet.Web.Models.AutomaticRules
{
    public class IndexModel
    {
        public IndexModel()
        {
            Status = new DropDownListViewStringModel
            {
                Items = new List<SelectListItem>
                {
                    new SelectListItem {Value = "", Text = $"--- {T.All} ---", Selected = true },
                    new SelectListItem {Value = "True", Text = T.ActiveP},
                    new SelectListItem {Value = "False", Text = T.Archived},
                }
            };
        }

        public int? CustomerId { get; set; }
        public Customer Customer { get; set; }

        [Display(Name = "Status", ResourceType = typeof(T)), MvcFilter("Filter", "Status")]
        public DropDownListViewStringModel Status { get; set; }
    }
}
