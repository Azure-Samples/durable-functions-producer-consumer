using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Consumer
{
    internal class CollectorMessage
    {
        public DateTime MessageProcessedTime { get; set; }
        public string CloudProvider { get; } = @"Azure";
        public string TestRun { get; set; }
        public string Trigger { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}