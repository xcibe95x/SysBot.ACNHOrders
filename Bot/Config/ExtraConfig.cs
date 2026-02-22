using System;

namespace SysBot.ACNHOrders
{
    [Serializable]
    public class ExtraConfig
    {
        public AnchorAutomationConfig AnchorAutomationConfig { get; set; } = new();
    }
}
