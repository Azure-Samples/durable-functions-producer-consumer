using Models;

namespace Consumer.Lambda
{
    class AwsCollectorMessage : CollectorMessage
    {
        public override string CloudProvider => @"AWS";
    }
}
