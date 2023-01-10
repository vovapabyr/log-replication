using HealthChecks.UI.Core;

namespace Master.Models
{
    public class HealthCheckPayload
    {
        public string Name { get; set; }

        public string Status { get; set; }

        public string Descriptions { get; set; }

        public UIHealthStatus GetStatus() => (UIHealthStatus)Enum.Parse(typeof(UIHealthStatus), Status);
    }
}
