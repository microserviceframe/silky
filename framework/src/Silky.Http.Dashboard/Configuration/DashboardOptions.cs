namespace Silky.Http.Dashboard.Configuration
{
    public class DashboardOptions
    {
        internal static string Dashboard = "Dashboard";

        public DashboardOptions()
        {
            PathMatch = "/dashboard";
            StatsPollingInterval = 2000;
        }

        public string PathMatch { get; set; }

        public int StatsPollingInterval { get; set; }

        public bool UseAuth { get; set; }

        public string LoginWebApi { get; set; }

        public bool DisplayWebApiInSwagger { get; set; }

        public string PathBase { get; set; }
    }
}