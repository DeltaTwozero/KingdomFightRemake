using UnityEngine;

public static class PlayerSettings
{
    public static class PlayerControls
    {
        private const string ToggleSprintKey = "PlayerControls.ToggleSprint";

        /// <summary>
        /// When true, pressing Sprint toggles running on/off.
        /// When false (default), Sprint must be held down to run.
        /// </summary>
        public static bool ToggleSprint
        {
            get => PlayerPrefs.GetInt(ToggleSprintKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(ToggleSprintKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }

    public static class Privacy
    {
        private const string HideInviteCodeKey = "Privacy.HideInviteCodeByDefault";

        /// <summary>
        /// When true (default), the invite code is hidden behind ••••••
        /// until the player clicks Hide/Show.
        /// </summary>
        public static bool HideInviteCodeByDefault
        {
            get => PlayerPrefs.GetInt(HideInviteCodeKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(HideInviteCodeKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
