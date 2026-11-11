// BuildInfo.cs - version shown on the title screen and written into every build.
namespace BlossomRally
{
    public static class BuildInfo
    {
        public const string Version = "0.10.0";
        public const string Channel = "Test build";
        /// <summary>Shown under the title so testers' screenshots say which build they played.</summary>
        public const string Label = Channel + " v" + Version;
        /// <summary>Where testers send feedback (edit before release, e.g. your landing page or a form).</summary>
        /// <summary>Weekly update promise shown on the title screen, in the pause menu and in the download notes.</summary>
        public const string UpdateNote = "New update every Thursday at 12 PM - fresh improvements every week!";
        public const string FeedbackHint = "Thanks for testing! Send feedback + screenshots (O = photo mode, F1 = FPS)";
    }
}
