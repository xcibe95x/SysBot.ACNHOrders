using System;

namespace SysBot.ACNHOrders
{
    [Serializable]
    public class GitHubConfig
    {
        /// <summary> When set to true, pushes the Dodo file contents to a GitHub repo whenever it changes. </summary>
        public bool PushDodoToGithub { get; set; } = false;

        /// <summary> GitHub repo in the format "owner/repo" or a full GitHub URL. </summary>
        public string GitHubDodoRepo { get; set; } = "";

        /// <summary> GitHub branch to commit to. </summary>
        public string GitHubDodoBranch { get; set; } = "main";

        /// <summary> Path in the repo where the Dodo file will be stored. </summary>
        public string GitHubDodoPath { get; set; } = "Dodo.txt";

        /// <summary> GitHub token used for authentication (fine-grained or classic PAT). </summary>
        public string GitHubToken { get; set; } = "";

        /// <summary> Commit message used when updating the Dodo file. </summary>
        public string GitHubDodoCommitMessage { get; set; } = "Update Dodo.txt";
    }
}
