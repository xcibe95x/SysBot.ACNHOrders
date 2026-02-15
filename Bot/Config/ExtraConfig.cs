using System;

namespace SysBot.ACNHOrders
{
    [Serializable]
    public class ExtraConfig
    {
        public GitHubConfig GitHubConfig { get; set; } = new();
        public AnchorAutomationConfig AnchorAutomationConfig { get; set; } = new();
    }
}
