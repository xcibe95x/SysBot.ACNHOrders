using System;

namespace SysBot.ACNHOrders
{
    [Serializable]
    public class AnchorAutomationConfig
    {
        /// <summary>
        /// When enabled, the bot will automatically refresh anchors as it naturally reaches known anchor points
        /// (house entry, airport entry, Orville counter, airport exit, drop zone).
        /// </summary>
        public bool AutoRefreshAnchors { get; set; } = false;

        /// <summary>
        /// When enabled, if anchor 0 is empty the bot will bootstrap it automatically
        /// after startup once Overworld is stable (character has loaded out of the house).
        /// </summary>
        public bool AutoBootstrapHouseAnchor { get; set; } = true;

        /// <summary>
        /// Maximum XY distance before an anchor is considered changed and gets rewritten.
        /// </summary>
        public float AnchorUpdateBuffer { get; set; } = 0.35f;

        /// <summary>
        /// When enabled, if anchors 1-4 are missing the bot will guide setup in logs and
        /// auto-capture each anchor when the player stands still at the requested spot.
        /// </summary>
        public bool AutoGuidedAnchorSetup { get; set; } = true;

        /// <summary>
        /// When enabled, guided setup captures anchors on a console key press instead of
        /// stability detection. Useful when running multiple bots and avoiding global Discord commands.
        /// </summary>
        public bool GuidedSetupUseConsoleKey { get; set; } = true;

        /// <summary>
        /// Console key to press for guided anchor capture (for example: F8, F9, Enter).
        /// </summary>
        public string GuidedSetupConsoleKey { get; set; } = "F8";

        /// <summary>
        /// Timeout in seconds while waiting for the guided console key.
        /// </summary>
        public int GuidedSetupConsoleTimeoutSeconds { get; set; } = 300;
    }
}
