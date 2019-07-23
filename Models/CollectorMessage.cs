using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Models
{
    public abstract class CollectorMessage
    {
        public abstract string CloudProvider { get; }

        public string TestRun { get; set; }
        public DateTime MessageProcessedTime { get; set; } = DateTime.UtcNow;
        public string Trigger { get; set; }
        public JObject Properties { get; set; }
        public int MessageNumber { get; set; }
        public DateTime PublishTime { get; set; }


        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}